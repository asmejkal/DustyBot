using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord.Rest;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Cross-cutting thread behavior that isn't tied to any single module's command surface:
    /// ThreadJoinService (Service/src/DustyBot.Service.New/Services/ThreadJoinService.cs) auto-joins every newly
    /// created public thread (never private ones), commands work the same inside a thread as in a regular
    /// channel, and IMessageGuildChannel-typed parameters (e.g. LogModule's "messages" target) accept a thread
    /// mention just like a channel mention, since threads implement that interface too.
    ///
    /// Threads created here aren't deleted afterward, like the run's main channel, so they can be reviewed in
    /// Discord alongside the rest of the run.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class ThreadBehaviorTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _output;

        public ThreadBehaviorTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task NewPublicThread_BotAutoJoins()
        {
            var thread = await Fixture.Admin.Client.CreatePublicThreadAsync(Fixture.ChannelId, "autojoin-thread-" + Guid.NewGuid().ToString("N")[..8]);

            // ThreadCreated is dispatched to the target bot and its own JoinThreadAsync call both happen
            // asynchronously server-side, with no event the harness can wait on instead - retry rather than
            // guessing a fixed delay.
            var joined = false;
            for (var attempt = 0; attempt < 5 && !joined; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(TimeSpan.FromSeconds(1.5));

                var members = await Fixture.Admin.Client.FetchThreadMembersAsync(thread.Id);
                joined = members.Any(m => m.Id == Options.TargetBotId);
            }

            Assert.True(joined, "Expected the target bot to have auto-joined the newly created public thread.");
        }

        [Fact]
        public async Task CommandInvokedInsideThread_RepliesNormally()
        {
            var thread = await Fixture.Admin.Client.CreatePublicThreadAsync(Fixture.ChannelId, "cmd-thread-" + Guid.NewGuid().ToString("N")[..8]);

            var sent = await Fixture.Admin.SendCommandAsync(thread.Id, $"{Options.CommandPrefix}avatar");
            var result = await Fixture.Admin.WaitForReplyAsync(Options.TargetBotId, thread.Id, sent.Id, AckTimeout);
            Assert.Single(result.Embeds);
        }

        [Fact]
        public async Task ChannelParameter_AcceptsThreadMention()
        {
            var thread = await Fixture.Admin.Client.CreatePublicThreadAsync(Fixture.ChannelId, "channel-param-thread-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var sent = await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages <#{thread.Id}>");
                var ack = await Fixture.Admin.WaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, sent.Id, AckTimeout);
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
            }
        }
    }
}
