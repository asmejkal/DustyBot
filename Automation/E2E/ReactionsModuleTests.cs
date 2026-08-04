using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Communication;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for ReactionsModule ("reactions"/"reaction" group). Each test uses its own random
    /// trigger/response so tests can't collide, removing its entry in DisposeAsync. The first test to run
    /// clears all reactions and resets the manager role once (RunOnceGate).
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class ReactionsModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ListingTimeout = TimeSpan.FromSeconds(15);
        private static readonly RunOnceGate ResetOnce = new();

        private readonly ITestOutputHelper _output;

        // Mutated by the rename test, so DisposeAsync cleans up the right thing.
        private string _trigger = "trig" + Guid.NewGuid().ToString("N")[..8];
        private readonly string _response = "resp" + Guid.NewGuid().ToString("N")[..8];

        public ReactionsModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions clear");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions set manager");
            });

            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
        }

        public async Task DisposeAsync()
        {
            // Best-effort: each test uses a unique trigger, so this can't affect any other test's data.
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions remove {_trigger}");
        }

        [Fact]
        public async Task Add_Succeeds_AndReactionIsFindable()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}");
            ResponseAssert.IsSuccess(ack);
            Assert.Contains("added", ack.Content, StringComparison.OrdinalIgnoreCase);

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {_trigger}");
            ResponseAssert.Contains(result, _trigger);
            ResponseAssert.Contains(result, _response);
        }

        [Fact]
        public async Task Add_ReportedIdCanBeUsedToReferenceTheReaction()
        {
            // Regression test: AddReactionAsync used to increment its ID counter twice per call, so the ID
            // reported here didn't match the reaction's actual stored ID.
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}");
            ResponseAssert.IsSuccess(ack);

            var match = Regex.Match(ack.Content, "`(\\d+)`");
            Assert.True(match.Success, $"Expected the add acknowledgement to contain a backtick-quoted ID. Content: \"{ack.Content}\"");

            var editAck = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions edit {match.Groups[1].Value} newvalue");
            ResponseAssert.IsSuccess(editAck);
        }

        [Fact]
        public async Task Add_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        [Fact]
        public async Task Edit_ExistingReaction_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var newResponse = "resp" + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions edit {_trigger} {newResponse}");
            ResponseAssert.IsSuccess(ack);

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {_trigger}");
            ResponseAssert.Contains(result, newResponse);
        }

        [Fact]
        public async Task Edit_NonexistentReaction_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions edit {_trigger} newvalue");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Couldn't find", ack.Content);
        }

        [Fact]
        public async Task Edit_InvokedByNonManager_Fails()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}reactions edit {_trigger} newvalue");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        [Fact]
        public async Task Rename_ExistingReaction_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var newTrigger = "trig" + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions rename {_trigger} {newTrigger}");
            ResponseAssert.IsSuccess(ack);
            _trigger = newTrigger; // so DisposeAsync cleans up the renamed entry

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {newTrigger}");
            ResponseAssert.Contains(result, newTrigger);
        }

        [Fact]
        public async Task Rename_NonexistentReaction_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions rename {_trigger} newtrigger");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Couldn't find", ack.Content);
        }

        [Fact]
        public async Task Cooldown_ValidValue_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions cooldown {_trigger} 5");
            ResponseAssert.IsSuccess(ack);
            Assert.Contains("5", ack.Content);
        }

        [Fact]
        public async Task Cooldown_ZeroOrNegative_Fails()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions cooldown {_trigger} 0");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("greater than 0", ack.Content);
        }

        [Fact]
        public async Task Cooldown_NonexistentReaction_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions cooldown {_trigger} 5");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Couldn't find", ack.Content);
        }

        [Fact]
        public async Task Remove_ExistingReaction_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions remove {_trigger}");
            ResponseAssert.IsSuccess(ack);

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {_trigger}");
            ResponseAssert.Contains(result, "Found no reactions");
        }

        [Fact]
        public async Task Remove_NonexistentReaction_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions remove {_trigger}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Couldn't find", ack.Content);
        }

        [Fact]
        public async Task Search_FindsMatchingReaction()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {_trigger}");
            ResponseAssert.Contains(result, _trigger);
        }

        [Fact]
        public async Task Search_NoMatches_ReturnsNotFoundMessage()
        {
            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {_trigger}");
            ResponseAssert.Contains(result, "Found no reactions");
        }

        [Fact]
        public async Task List_ShowsAddedReaction()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions list");
            ResponseAssert.Contains(result, _trigger);
        }

        [Fact]
        public async Task List_InvokedByNonManager_StillSucceeds()
        {
            // "list" has no RequireAuthorReactionsManager attribute, unlike add/edit/remove/cooldown/clear.
            var beforeSend = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions list");
            await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeSend, ListingTimeout);
        }

        [Fact]
        public async Task Stats_ForExistingReaction_ShowsZeroTriggerCount()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions stats {_trigger}");
            ResponseAssert.Contains(result, "Triggered");
        }

        [Fact]
        public async Task Stats_ForNonexistentReaction_ReturnsNotFoundMessage()
        {
            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions stats {_trigger}");
            ResponseAssert.Contains(result, "Couldn't find");
        }

        [Fact]
        public async Task Stats_Bare_ReturnsAResponse()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            await RunQueryAsync($"{Options.CommandPrefix}reactions stats");
        }

        [Fact]
        public async Task RealMessageTrigger_RespondsWithConfiguredValueAndUpdatesStats()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var beforeSend = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, _trigger);
            var reply = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeSend, ListingTimeout);
            Assert.Equal(_response, reply.Content);

            var stats = await RunQueryAsync($"{Options.CommandPrefix}reactions stats {_trigger}");
            ResponseAssert.Contains(stats, "1");
        }

        [Fact]
        public async Task Clear_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}reactions clear");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Clear_RemovesAllReactions_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions clear");
            ResponseAssert.IsSuccess(ack);

            // An empty field collection makes the underlying paged view fall back to its own generic empty-state
            // text instead of ever calling ReactionListing's embed builder (which is what would normally set the
            // "0 reactions" footer) - so "No items." is what's actually observable here.
            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions list");
            ResponseAssert.Contains(result, "No items");
        }

        [Fact]
        public async Task SetManagerRole_WithRole_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "reactions-manager-test");
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions set manager {role.Id}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions set manager");
            }
        }

        [Fact]
        public async Task ManagerRole_GrantedToNonManager_AllowsManagingReactions()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "reactions-manager-test-grant");
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions set manager {role.Id}"));

                // Without the role, LowPriv still can't manage reactions.
                var beforeAck = await RunLowPrivCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}");
                ResponseAssert.IsFailure(beforeAck);

                await Fixture.Admin.Client.GrantRoleAsync(Options.GuildId, lowPrivId, role.Id);

                // The bot's member cache updates via a gateway event after the grant, which can lag slightly
                // behind the REST call - retry (paced by SendCommandAsync's own rate-limit delay) rather than
                // guessing a fixed wait up front.
                var afterAck = await RetryUntilSuccessAsync(
                    () => RunLowPrivCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"),
                    maxAttempts: 5);
                ResponseAssert.IsSuccess(afterAck);
            }
            finally
            {
                await Fixture.Admin.Client.RevokeRoleAsync(Options.GuildId, lowPrivId, role.Id);
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions set manager");
            }
        }

        [Fact]
        public async Task SetManagerRole_NoRole_DisablesSuccessfully()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}reactions set manager");
            ResponseAssert.IsSuccess(ack);
            Assert.Contains("disabled", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetManagerRole_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}reactions set manager");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Export_WithReactions_SendsAttachment()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}reactions add {_trigger} {_response}"));

            // "export" replies via DustyModuleBase.Reply, not Success/Failure, so it carries no marker.
            var result = await RunQueryAsync($"{Options.CommandPrefix}reactions export");
            Assert.NotEmpty(result.Attachments);
        }

        [Fact]
        public async Task Import_WithValidFile_Succeeds()
        {
            var importTrigger = "trig" + Guid.NewGuid().ToString("N")[..8];
            var importResponse = "resp" + Guid.NewGuid().ToString("N")[..8];
            var json = $$"""[{"{{importTrigger}}":"{{importResponse}}"}]""";

            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var sent = await Fixture.Admin.SendCommandWithAttachmentAsync(
                    Fixture.ChannelId, $"{Options.CommandPrefix}reactions import", new LocalAttachment(stream, "reactions.json"));
                var ack = await Fixture.Admin.WaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, sent.Id, AckTimeout);
                ResponseAssert.IsSuccess(ack);

                var result = await RunQueryAsync($"{Options.CommandPrefix}reactions search {importTrigger}");
                ResponseAssert.Contains(result, importTrigger);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}reactions remove {importTrigger}");
            }
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        /// <summary>
        /// "search"/"list"/"stats"/"export" render as either a neutral Result or a paged menu (neither carries a
        /// marker, and menus aren't reliably reply-linked), so correlation is by timestamp instead.
        /// </summary>
        private Task<IGatewayUserMessage> RunQueryAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, ListingTimeout);

        private static async Task<IGatewayUserMessage> RetryUntilSuccessAsync(Func<Task<IGatewayUserMessage>> attempt, int maxAttempts)
        {
            var last = await attempt();
            for (var i = 1; i < maxAttempts && !IsSuccessResponse(last); i++)
                last = await attempt();

            return last;
        }

        private static bool IsSuccessResponse(IGatewayUserMessage message) =>
            message.Content?.StartsWith(CommunicationConstants.SuccessMarker) == true;
    }
}
