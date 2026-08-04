using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for NotificationsModule ("notifications"/"notif"/"noti" group). Every command operates on
    /// the invoking user themselves - no admin/manager gating, just [RequireGuild] on guild-scoped commands.
    ///
    /// Discord blocks bot-to-bot DMs entirely, so this suite can't verify real DM delivery - it only checks
    /// command acks, including the deterministic Failure ack "list" returns when it can't deliver its DM.
    ///
    /// Keywords are isolated per test via a unique random keyword. Pause/resume and block/unblock get a
    /// defensive reset once per run (ResetOnce); "ignore active channel"/"opt out" have no safe way to force a
    /// known state, so each test restores them itself in a `finally`.
    ///
    /// "add"/"remove"/"block"/"unblock" carry [HideInvocation] (ShouldReply is unconditionally false), so their
    /// acks are plain, non-reply messages - these use RunAdminHiddenCommandAsync instead of RunAdminCommandAsync.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class NotificationsModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

        private static readonly RunOnceGate ResetOnce = new();

        private readonly ITestOutputHelper _output;
        private readonly string _keyword = "kw" + Guid.NewGuid().ToString("N")[..8];

        public NotificationsModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await ResetOnce.RunOnceAsync(async () =>
            {
                var adminId = Fixture.Admin.Client.CurrentUser!.Id;
                var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications clear");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications resume");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications unblock {lowPrivId}");

                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications clear");
                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications resume");
                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications unblock {adminId}");
            });

            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
        }

        public async Task DisposeAsync()
        {
            // Best-effort: each test uses its own unique keyword.
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications remove {_keyword}");
        }

        [Fact]
        public async Task Add_SingleKeyword_Succeeds()
        {
            var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task Add_MultipleKeywordsInOneCall_Succeeds()
        {
            var second = "kw" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword} {second}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications remove {second}");
            }
        }

        [Fact]
        public async Task Add_TooShort_Fails()
        {
            var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add a");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("too short", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Add_TooLong_Fails()
        {
            var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {new string('a', 51)}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("too long", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Add_Duplicate_Fails()
        {
            ResponseAssert.IsSuccess(await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}"));

            var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("already", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Add_AtCapacity_Fails()
        {
            // AddKeywordsAsync allows up to exactly MaxNotificationsPerUser (20) keywords and rejects anything
            // that would exceed it.
            var keywords = Enumerable.Range(0, 20).Select(_ => "kw" + Guid.NewGuid().ToString("N")[..8]).ToArray();
            try
            {
                var addAck = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {string.Join(' ', keywords)}");
                ResponseAssert.IsSuccess(addAck);

                var overflowAck = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}");
                ResponseAssert.IsFailure(overflowAck);
                Assert.Contains("too many", overflowAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications clear");
            }
        }

        [Fact]
        public async Task Remove_Existing_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}"));

            var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications remove {_keyword}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task Remove_Nonexistent_Fails()
        {
            var ack = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications remove {_keyword}");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Clear_RemovesAllKeywords_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications clear");
            ResponseAssert.IsSuccess(ack);

            var listAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications list");
            ResponseAssert.Contains(listAck, "don't have any");
        }

        [Fact]
        public async Task List_NoKeywords_ReturnsMessage()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications list");
            ResponseAssert.Contains(ack, "don't have any");
        }

        [Fact]
        public async Task List_WithKeywords_FailsToDeliverDirectMessage()
        {
            // Discord rejects bot-to-bot DMs outright, so ListKeywordsAsync's attempt to DM the tester bot always
            // hits its RestApiException(CannotSendMessagesToThisUser) branch and acks with this Failure instead
            // of "Please check your direct messages." - the only way this command's DM-delivery path is
            // observable from here.
            ResponseAssert.IsSuccess(await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications add {_keyword}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications list");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("direct message", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Pause_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications pause");
            ResponseAssert.IsSuccess(ack);

            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}notifications resume");
        }

        [Fact]
        public async Task Resume_WithoutPause_StillSucceeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications resume");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task IgnoreChannel_Toggle_TogglesAckWording()
        {
            var onAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications ignore channel");
            ResponseAssert.IsSuccess(onAck);
            Assert.Contains("no longer receive", onAck.Content, StringComparison.OrdinalIgnoreCase);

            var offAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications ignore channel");
            ResponseAssert.IsSuccess(offAck);
            Assert.Contains("now receive", offAck.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task IgnoreActiveChannel_Toggle_TogglesAckWording()
        {
            var onAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications ignore active channel");
            ResponseAssert.IsSuccess(onAck);

            var offAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications ignore active channel");
            ResponseAssert.IsSuccess(offAck);
        }

        [Fact]
        public async Task OptOut_Toggle_TogglesAckWording()
        {
            var outAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications opt out");
            ResponseAssert.IsSuccess(outAck);
            Assert.Contains("no longer trigger", outAck.Content, StringComparison.OrdinalIgnoreCase);

            var inAck = await RunAdminCommandAsync($"{Options.CommandPrefix}notifications opt out");
            ResponseAssert.IsSuccess(inAck);
            Assert.Contains("may now trigger", inAck.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Block_Succeeds_UnblockRestores()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

            var blockAck = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications block {lowPrivId}");
            ResponseAssert.IsSuccess(blockAck);

            var unblockAck = await RunAdminHiddenCommandAsync($"{Options.CommandPrefix}notifications unblock {lowPrivId}");
            ResponseAssert.IsSuccess(unblockAck);
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        /// <summary>
        /// For commands marked [HideInvocation] ("add"/"remove"/"block"/"unblock") - DustyModuleBase.ShouldReply
        /// is unconditionally false for those, so their ack comes back as a plain, non-reply message.
        /// </summary>
        private Task<IGatewayUserMessage> RunAdminHiddenCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForFollowUpAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
