using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Core.Parsing;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Communication
{
    internal class Communicator : ICommunicator, IDisposable
    {
        private sealed class PaginatedMessageContext : IDisposable
        {
            public int CurrentPage
            {
                get => _currentPage;
                set
                {
                    if (value >= Pages.Count)
                        throw new ArgumentException();
                    else
                        _currentPage = value;
                }
            }

            public int TotalPages => _pages.Count;
            public IReadOnlyCollection<Page> Pages { get => _pages; }

            public bool ControlledByInvoker => InvokerUserId != 0;
            public ulong InvokerUserId { get; }
            public bool Resend { get; set; }

            public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);

            public DateTime ExpirationDate { get; private set; } = DateTime.Now + PaginatedMessageLife;

            private int _currentPage = 0;
            private List<Page> _pages;

            public PaginatedMessageContext(PageCollection pages, ulong messageOwner = 0, bool resend = false)
            {
                _pages = new List<Page>(pages);
                InvokerUserId = messageOwner;
                Resend = resend;
            }

            public void ExtendLife() => ExpirationDate = DateTime.Now + PaginatedMessageLife;

            public void Dispose()
            {
                ((IDisposable)Lock).Dispose();
            }
        }

        public const string SuccessMarker = "\u2705";
        public const string FailureMarker = "\u26D4";
        public const string QuestionMarker = "\u2754";
        public static readonly Color ColorSuccess = Color.Green;
        public static readonly Color ColorError = Color.Red;
        public static readonly IEmote ArrowLeft = new Emoji("\u2B05\uFE0F");
        public static readonly IEmote ArrowRight = new Emoji("\u27A1\uFE0F");
        public static readonly TimeSpan PaginatedMessageLife = TimeSpan.FromHours(48);

        private readonly BaseSocketClient _client;
        private readonly ILogger<Communicator> _logger;
        private readonly Dictionary<ulong, PaginatedMessageContext> _paginatedMessages = new Dictionary<ulong, PaginatedMessageContext>();

        public Communicator(BaseSocketClient client, ILogger<Communicator> logger)
        {
            _client = client;
            _logger = logger;

            _client.ReactionAdded += HandleReactionAdded;
            _client.MessageDeleted += HandleMessageDeleted;
        }

        public async Task SendMessage(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false)
        {
            // Set footers where applicable (only if we have more than one page or if the only page has no custom footer)
            if (pages.Count > 1 || (pages.Count == 1 && pages[0].Embed != null && string.IsNullOrEmpty(pages[0].Embed.Footer?.Text)))
            {
                for (int i = 0; i < pages.Count; ++i)
                {
                    if (pages[i].Embed == null)
                        continue;

                    var footer = $"Page {i + 1} of {pages.Count}" + (!string.IsNullOrEmpty(pages[i].Embed.Footer?.Text) ? $" • {pages[i].Embed.Footer.Text}" : "");
                    if (pages[i].Embed.Footer != null)
                        pages[i].Embed.Footer.Text = footer;
                    else
                        pages[i].Embed.WithFooter(x => x.WithText(footer));
                }
            }

            // Send the first page
            var result = await channel.SendMessageAsync(pages.First().Content.Sanitise(), false, pages.First().Embed?.Build()).ConfigureAwait(false);

            // If there's more pages, save a context
            if (pages.Count > 1)
            {
                var context = new PaginatedMessageContext(pages, messageOwner, resend);
                lock (_paginatedMessages)
                {
                    _paginatedMessages.Add(result.Id, context);
                }

                await DiscordHelpers.EnsureBotPermissions(channel, ChannelPermission.AddReactions);
                await result.AddReactionAsync(ArrowLeft);
                await result.AddReactionAsync(ArrowRight);

                // Clean old messages, to avoid tracking too many unnecessary messages when the bots runs for a long time
                CleanupPaginatedMessages();
            }
        }

        public Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text) =>
            SendMessage(channel, text, null);

        public Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, Embed embed) =>
            SendMessage(channel, "", embed);

        public async Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Embed embed)
        {
            var result = new List<IUserMessage>();
            var chunks = text.ChunkifyByLines(DiscordConfig.MaxMessageSize).ToList();
            foreach (var chunk in chunks.SkipLast())
                result.Add(await channel.SendMessageAsync(chunk.ToString().Sanitise()));

            if (chunks.Any())
                result.Add(await channel.SendMessageAsync(chunks.Last().ToString().Sanitise(), embed: embed));
            else
                result.Add(await channel.SendMessageAsync("", embed: embed));

            return result;
        }

        public async Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0)
        {
            if (maxDecoratorOverhead >= DiscordConfig.MaxMessageSize)
                throw new ArgumentException($"Argument {nameof(maxDecoratorOverhead)} may not exceed the message length limit, {DiscordConfig.MaxMessageSize}.", nameof(maxDecoratorOverhead));

            var result = new List<IUserMessage>();
            foreach (var chunk in text.ChunkifyByLines(DiscordConfig.MaxMessageSize - maxDecoratorOverhead))
                result.Add(await channel.SendMessageAsync(chunkDecorator(chunk.ToString()).Sanitise()));

            return result;
        }

        public string FormatSuccess(string message) => SuccessMarker + " " + message;
        public string FormatFailure(string message) => FailureMarker + " " + message;
        public string FormatQuestion(string message) => QuestionMarker + " " + message;

        public async Task<ICollection<IUserMessage>> CommandReplySuccess(IMessageChannel channel, string message, Embed embed = null)
            => await SendMessage(channel, FormatSuccess(message), embed: embed);

        public async Task<ICollection<IUserMessage>> CommandReplyError(IMessageChannel channel, string message)
            => await SendMessage(channel, FormatFailure(message));

        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message)
            => await SendMessage(channel, message);

        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, Embed embed)
            => await SendMessage(channel, "", embed);

        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Embed embed)
            => await SendMessage(channel, message, embed);

        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0)
            => await SendMessage(channel, message, chunkDecorator, maxDecoratorOverhead);

        public async Task CommandReply(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false)
            => await SendMessage(channel, pages, messageOwner, resend);

        public async Task<ICollection<IUserMessage>> CommandReplyMissingPermissions(IMessageChannel channel, CommandInfo command, IEnumerable<GuildPermission> missingPermissions, string message = null)
            => await CommandReplyError(channel, string.Format(Properties.Resources.Command_MissingPermissions, missingPermissions.WordJoin(Properties.Resources.Common_WordListSeparator, Properties.Resources.Common_WordListLastSeparator)) + " " + message ?? "");

        public async Task<ICollection<IUserMessage>> CommandReplyMissingBotAccess(IMessageChannel channel, CommandInfo command)
            => await CommandReplyError(channel, Properties.Resources.Command_MissingBotAccess);

        public async Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, CommandInfo command)
            => await CommandReplyError(channel, Properties.Resources.Command_MissingBotPermissionsUnknown);

        public async Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, CommandInfo command, IEnumerable<GuildPermission> missingPermissions)
            => await CommandReplyError(channel, string.Format(missingPermissions.Count() > 1 ? Properties.Resources.Command_MissingBotPermissionsMultiple : Properties.Resources.Command_MissingBotPermissions, missingPermissions.WordJoin(Properties.Resources.Common_WordListSeparator, Properties.Resources.Common_WordListLastSeparator)));

        public async Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, CommandInfo command, IEnumerable<ChannelPermission> missingPermissions)
            => await CommandReplyError(channel, string.Format(missingPermissions.Count() > 1 ? Properties.Resources.Command_MissingBotPermissionsMultiple : Properties.Resources.Command_MissingBotPermissions, missingPermissions.WordJoin(Properties.Resources.Common_WordListSeparator, Properties.Resources.Common_WordListLastSeparator)));

        public async Task<ICollection<IUserMessage>> CommandReplyNotOwner(IMessageChannel channel, CommandInfo command)
            => await CommandReplyError(channel, Properties.Resources.Command_NotOwner);

        public async Task<ICollection<IUserMessage>> CommandReplyIncorrectParameters(IMessageChannel channel, CommandInfo command, string explanation, string commandPrefix, bool showUsage = true)
        {
            var embed = new EmbedBuilder()
                    .WithTitle(Properties.Resources.Command_Usage)
                    .WithDescription(BuildCommandUsage(command, commandPrefix))
                    .WithFooter(Properties.Resources.Command_UsageFooter);

            if (string.IsNullOrEmpty(explanation))
                explanation = Properties.Resources.Command_IncorrectParameters;

            return new[] { await channel.SendMessageAsync(FormatFailure(explanation.Sanitise()), false, showUsage ? embed.Build() : null) };
        }

        public async Task<ICollection<IUserMessage>> CommandReplyUnclearParameters(IMessageChannel channel, CommandInfo command, string explanation, string commandPrefix, bool showUsage = true)
        {
            var embed = new EmbedBuilder()
                    .WithTitle(Properties.Resources.Command_Usage)
                    .WithDescription(BuildCommandUsage(command, commandPrefix))
                    .WithFooter(Properties.Resources.Command_UsageFooter);

            return new[] { await channel.SendMessageAsync(FormatQuestion(explanation.Sanitise()), false, showUsage ? embed.Build() : null) };
        }

        public async Task<ICollection<IUserMessage>> CommandReplyDirectMessageOnly(IMessageChannel channel, CommandInfo command) =>
            await CommandReplyError(channel, Properties.Resources.Command_DirectMessageOnly);

        public async Task<ICollection<IUserMessage>> CommandReplyGenericFailure(IMessageChannel channel) =>
            await CommandReplyError(channel, Properties.Resources.Command_GenericFailure);
        
        public string BuildCommandUsage(CommandInfo command, string commandPrefix)
        {
            string usage = $"{commandPrefix}{command.PrimaryUsage.InvokeUsage}";
            foreach (var param in command.Parameters.Where(x => !x.Flags.HasFlag(ParameterFlags.Hidden)))
            {
                string tmp = param.Name;
                if (param.Flags.HasFlag(ParameterFlags.Remainder))
                    tmp += "...";

                if (param.Flags.HasFlag(ParameterFlags.Optional))
                    tmp = $"[{tmp}]";

                usage += $" `{tmp}`";
            }

            string paramDescriptions = string.Empty;
            foreach (var param in command.Parameters.Where(x => !x.Flags.HasFlag(ParameterFlags.Hidden) && !string.IsNullOrWhiteSpace(x.GetDescription(commandPrefix))))
            {
                string tmp = $"● `{param.Name}` ‒ ";
                if (param.Flags.HasFlag(ParameterFlags.Optional))
                    tmp += "optional; ";

                tmp += param.GetDescription(commandPrefix);
                paramDescriptions += string.IsNullOrEmpty(paramDescriptions) ? tmp : "\n" + tmp;
            }

            var examples = command.Examples
                .Select(x => $"{commandPrefix}{command.PrimaryUsage.InvokeUsage} {x}")
                .DefaultIfEmpty()
                .Aggregate((x, y) => x + "\n" + y);

            return usage +
                (string.IsNullOrWhiteSpace(paramDescriptions) ? string.Empty : "\n\n" + paramDescriptions) +
                (string.IsNullOrWhiteSpace(command.GetComment(commandPrefix)) ? string.Empty : "\n\n" + command.GetComment(commandPrefix)) +
                (string.IsNullOrWhiteSpace(examples) ? string.Empty : "\n\n__Examples:__\n" + examples);
        }

        public void Dispose()
        {
            _client.ReactionAdded -= HandleReactionAdded;
            _client.MessageDeleted -= HandleMessageDeleted;

            CleanupPaginatedMessages();
        }

        private Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                        return;

                    // Check for page arrows
                    if (reaction.Emote.Name != ArrowLeft.Name && reaction.Emote.Name != ArrowRight.Name)
                        return;

                    // Lock and check if we have a page context for this message                        
                    PaginatedMessageContext context;
                    lock (_paginatedMessages)
                    {
                        if (!_paginatedMessages.TryGetValue(message.Id, out context))
                            return;
                    }

                    try
                    {
                        await context.Lock.WaitAsync();

                        // If requested, only allow the original invoker of the command to flip pages
                        var concMessage = await message.GetOrDownloadAsync();
                        if (context.ControlledByInvoker && reaction.UserId != context.InvokerUserId)
                            return;

                        // Message was touched -> refresh expiration date
                        context.ExtendLife();

                        // Calculate new page index and check bounds
                        var newPage = context.CurrentPage + (reaction.Emote.Name == ArrowLeft.Name ? -1 : 1);
                        if (newPage < 0 || newPage >= context.TotalPages)
                        {
                            await RemovePageReaction(concMessage, reaction.Emote, reaction.User.Value);
                            return;
                        }

                        // Modify or resend message
                        var newMessage = context.Pages.ElementAt(newPage);
                        if (context.Resend)
                        {
                            await concMessage.DeleteAsync();
                            var result = await concMessage.Channel.SendMessageAsync(newMessage.Content?.Sanitise(), false, newMessage.Embed?.Build());

                            lock (_paginatedMessages)
                            {
                                _paginatedMessages.Add(result.Id, context);
                            }

                            await result.AddReactionAsync(ArrowLeft);
                            await result.AddReactionAsync(ArrowRight);
                        }
                        else
                        {
                            await concMessage.ModifyAsync(x => 
                            { 
                                x.Content = newMessage.Content; 
                                x.Embed = newMessage.Embed?.Build(); 
                            });

                            await RemovePageReaction(concMessage, reaction.Emote, reaction.User.Value);
                        }

                        // Update context
                        context.CurrentPage = newPage;
                    }
                    finally
                    {
                        context.Lock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.WithScope(channel, message.Id).LogError(ex, "Failed to flip a page for PaginatedMessage");
                }
            });

            return Task.CompletedTask;
        }

        private Task HandleMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            try
            {
                lock (_paginatedMessages)
                {
                    if (_paginatedMessages.Remove(message.Id, out var context))
                        context.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.WithScope(channel, message.Id).LogError(ex, "Failed to remove a deleted message from paginated messages context");
            }

            return Task.CompletedTask;
        }

        private async Task RemovePageReaction(IUserMessage message, IEmote emote, IUser user)
        {
            try
            {
                await message.RemoveReactionAsync(emote, user);
            }
            catch (Discord.Net.HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Missing permission, ignore
            }
            catch (Exception ex)
            {
                _logger.WithScope(message).LogError(ex, "Failed to remove a reaction for PaginatedMessage.");
            }
        }

        private void CleanupPaginatedMessages()
        {
            lock (_paginatedMessages)
            {
                foreach (var removed in _paginatedMessages.RemoveAll((x, y) => y.ExpirationDate < DateTime.Now))
                    removed.Value.Dispose();
            }
        }
    }
}
