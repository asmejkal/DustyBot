using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Hosting;
using Disqord.Rest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DustyBot.Automation.E2E.Harness
{
    /// <summary>
    /// A minimal Discord client for one "tester" bot account: connects, sends command messages, and lets tests
    /// await messages the target bot sends back. Deliberately bypasses the command framework entirely - it only
    /// needs to speak raw gateway/REST, since it's driving DustyBot as an external, black-box actor.
    /// </summary>
    public sealed class TestDiscordClient : IAsyncDisposable
    {
        // Keeps every tester account comfortably under DustyBotSharder's default per-user rate limit
        // (5 commands / 7.5s, see DustyBotSharder.SetDefaultRateLimits).
        private static readonly TimeSpan MinSendInterval = TimeSpan.FromSeconds(1.6);

        private readonly IHost _host;
        private readonly List<Waiter> _waiters = new();
        private readonly object _waitersLock = new();
        private DateTimeOffset _lastSendAt = DateTimeOffset.MinValue;

        private TestDiscordClient(IHost host, DiscordClient client)
        {
            _host = host;
            Client = client;
            Client.MessageReceived += OnMessageReceivedAsync;
        }

        public DiscordClient Client { get; }

        public static async Task<TestDiscordClient> ConnectAsync(string token, CancellationToken ct)
        {
            var host = new HostBuilder()
                .ConfigureServices(services => services.AddLogging(builder => builder.ClearProviders()))
                .ConfigureDiscordClient((_, config) =>
                {
                    config.Token = token;
                    config.Intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent;
                })
                .Build();

            await host.StartAsync(ct);

            var client = host.Services.GetRequiredService<DiscordClient>();
            await client.WaitUntilReadyAsync(ct);

            return new TestDiscordClient(host, client);
        }

        public Task<IUserMessage> SendCommandAsync(Snowflake channelId, string content, CancellationToken ct = default)
            => SendMessageAsync(channelId, content, ct);

        public Task<IUserMessage> SendMessageAsync(Snowflake channelId, string content, CancellationToken ct = default)
            => SendAsync(channelId, new LocalMessage().WithContent(content), ct);

        public Task<IUserMessage> SendEmbedAsync(Snowflake channelId, LocalEmbed embed, CancellationToken ct = default)
            => SendAsync(channelId, new LocalMessage().WithEmbeds(embed), ct);

        public Task<IUserMessage> SendCommandWithAttachmentAsync(Snowflake channelId, string content, LocalAttachment attachment, CancellationToken ct = default)
            => SendAsync(channelId, new LocalMessage().WithContent(content).WithAttachments(attachment), ct);

        private async Task<IUserMessage> SendAsync(Snowflake channelId, LocalMessage message, CancellationToken ct)
        {
            var sinceLastSend = DateTimeOffset.UtcNow - _lastSendAt;
            if (sinceLastSend < MinSendInterval)
                await Task.Delay(MinSendInterval - sinceLastSend, ct);

            var sent = await Client.SendMessageAsync(channelId, message, cancellationToken: ct);
            _lastSendAt = DateTimeOffset.UtcNow;
            return sent!;
        }

        /// <summary>
        /// Waits for a message from <paramref name="authorId"/> in <paramref name="channelId"/> that is a reply
        /// to <paramref name="commandMessageId"/> - this is how DustyModuleBase.Success/Failure acknowledge a
        /// command (see DustyModuleBase.cs, WithReply(Context.Message.Id, ...)).
        /// </summary>
        public Task<IGatewayUserMessage> WaitForReplyAsync(
            Snowflake authorId, Snowflake channelId, Snowflake commandMessageId, TimeSpan timeout, CancellationToken ct = default)
            => WaitForMessageAsync(
                m => m.ChannelId == channelId && m.Author.Id == authorId && m.Reference?.MessageId == commandMessageId,
                timeout,
                ct);

        /// <summary>
        /// Waits for a new (non-reply) message from <paramref name="authorId"/> sent after <paramref name="after"/> -
        /// used for commands like "greet test" whose ack is empty (Success() with no content) and whose only
        /// observable effect is GreetByeSender posting the actual greet/bye message directly.
        /// </summary>
        public Task<IGatewayUserMessage> WaitForFollowUpMessageAsync(
            Snowflake authorId, Snowflake channelId, DateTimeOffset after, TimeSpan timeout, CancellationToken ct = default)
            => WaitForMessageAsync(
                m => m.ChannelId == channelId && m.Author.Id == authorId && m.Reference is null && m.Id.CreatedAt >= after,
                timeout,
                ct);

        /// <summary>
        /// Waits for any new message from <paramref name="authorId"/> sent after <paramref name="after"/>,
        /// regardless of whether it's a reply - used for commands whose response isn't reliably reply-linked
        /// (e.g. paged/menu results built via DustyModuleBase.Pages/View, like YouTubeModule's "views").
        /// </summary>
        public Task<IGatewayUserMessage> WaitForAnyMessageAsync(
            Snowflake authorId, Snowflake channelId, DateTimeOffset after, TimeSpan timeout, CancellationToken ct = default)
            => WaitForMessageAsync(
                m => m.ChannelId == channelId && m.Author.Id == authorId && m.Id.CreatedAt >= after,
                timeout,
                ct);

        /// <summary>
        /// Sends a command and waits for a reply-linked ack (DustyModuleBase.Success/Failure) - the common case
        /// for already-migrated DustyBot.Service.New modules. See WaitForReplyAsync for why this correlation
        /// works.
        /// </summary>
        public async Task<IGatewayUserMessage> RunAndWaitForReplyAsync(
            Snowflake targetBotId, Snowflake channelId, string commandText, TimeSpan timeout, CancellationToken ct = default)
        {
            var sent = await SendCommandAsync(channelId, commandText, ct);
            return await WaitForReplyAsync(targetBotId, channelId, sent.Id, timeout, ct);
        }

        /// <summary>
        /// Sends a command and waits for any new message afterward, regardless of reply-linking - the common
        /// case for not-yet-migrated old-stack modules (never reply-linked) and paged/menu results on either
        /// stack. See WaitForAnyMessageAsync.
        /// </summary>
        public async Task<IGatewayUserMessage> RunAndWaitForAnyMessageAsync(
            Snowflake targetBotId, Snowflake channelId, string commandText, TimeSpan timeout, CancellationToken ct = default)
        {
            var before = DateTimeOffset.UtcNow;
            await SendCommandAsync(channelId, commandText, ct);
            return await WaitForAnyMessageAsync(targetBotId, channelId, before, timeout, ct);
        }

        /// <summary>
        /// Sends a command and waits for a non-reply follow-up message - for [HideInvocation] commands and bare
        /// Success() acks whose only observable effect is a separate message. See WaitForFollowUpMessageAsync.
        /// </summary>
        public async Task<IGatewayUserMessage> RunAndWaitForFollowUpAsync(
            Snowflake targetBotId, Snowflake channelId, string commandText, TimeSpan timeout, CancellationToken ct = default)
        {
            var before = DateTimeOffset.UtcNow;
            await SendCommandAsync(channelId, commandText, ct);
            return await WaitForFollowUpMessageAsync(targetBotId, channelId, before, timeout, ct);
        }

        public Task<IGatewayUserMessage> WaitForMessageAsync(
            Func<IGatewayUserMessage, bool> predicate, TimeSpan timeout, CancellationToken ct = default)
        {
            var waiter = new Waiter(predicate);
            lock (_waitersLock)
                _waiters.Add(waiter);

            var cts = ct.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(ct) : new CancellationTokenSource();
            cts.CancelAfter(timeout);
            cts.Token.Register(() =>
            {
                waiter.Completion.TrySetException(new TimeoutException($"Timed out after {timeout} waiting for a matching message."));
                lock (_waitersLock)
                    _waiters.Remove(waiter);

                cts.Dispose();
            });

            return waiter.Completion.Task;
        }

        private Task OnMessageReceivedAsync(object? sender, MessageReceivedEventArgs e)
        {
            if (e.Message is not IGatewayUserMessage message)
                return Task.CompletedTask;

            List<Waiter>? matched = null;
            lock (_waitersLock)
            {
                for (var i = _waiters.Count - 1; i >= 0; i--)
                {
                    if (!_waiters[i].Predicate(message))
                        continue;

                    (matched ??= new List<Waiter>()).Add(_waiters[i]);
                    _waiters.RemoveAt(i);
                }
            }

            if (matched != null)
            {
                foreach (var waiter in matched)
                    waiter.Completion.TrySetResult(message);
            }

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            Client.MessageReceived -= OnMessageReceivedAsync;
            await _host.StopAsync();
            _host.Dispose();
        }

        private sealed class Waiter
        {
            public Waiter(Func<IGatewayUserMessage, bool> predicate)
            {
                Predicate = predicate;
                Completion = new TaskCompletionSource<IGatewayUserMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public Func<IGatewayUserMessage, bool> Predicate { get; }

            public TaskCompletionSource<IGatewayUserMessage> Completion { get; }
        }
    }
}
