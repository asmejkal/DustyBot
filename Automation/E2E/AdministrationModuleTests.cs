using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Http;
using Disqord.Rest;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for AdministrationModule ("Mod" module, old stack - not yet migrated). Acks are correlated
    /// by timestamp (WaitForAnyMessageAsync), not WaitForReplyAsync, since the old framework never reply-links.
    ///
    /// "ban" is exercised against the low-privilege tester bot: ban it for real, confirm it, immediately unban
    /// it via a direct REST call (there's no unban command), then wait for a human to re-invite it - a ban
    /// always removes guild membership and there's no programmatic way to restore that.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class AdministrationModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MuteEffectTimeout = TimeSpan.FromSeconds(20);

        private readonly ITestOutputHelper _output;

        public AdministrationModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task Say_ToAnotherChannel_SendsMessageAndAcks()
        {
            await using var target = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "say-target-" + Guid.NewGuid().ToString("N")[..8]);
            var content = "say test " + Guid.NewGuid().ToString("N")[..8];

            var before = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}say <#{target.Id}> {content}");

            var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            ResponseAssert.IsSuccess(ack);

            var posted = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, target.Id, before, AckTimeout);
            Assert.Equal(content, posted.Content);
        }

        [Fact]
        public async Task Say_ToSameChannel_SendsMessageWithoutAck()
        {
            var content = "say test " + Guid.NewGuid().ToString("N")[..8];
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}say <#{Fixture.ChannelId}> {content}");
            Assert.Equal(content, result.Content);
        }

        [Fact]
        public async Task Say_WithoutMessageOrAttachment_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}say <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Say_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}say <#{Fixture.ChannelId}> hi");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SayEmbed_Succeeds()
        {
            await using var target = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "say-embed-target-" + Guid.NewGuid().ToString("N")[..8]);
            var marker = Guid.NewGuid().ToString("N")[..8];

            var before = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}say embed <#{target.Id}> Title: Test {marker}\n\nDescription: Body {marker}");

            var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            ResponseAssert.IsSuccess(ack);

            var posted = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, target.Id, before, AckTimeout);
            var embed = Assert.Single(posted.Embeds);
            Assert.Contains(marker, embed.Title);
            Assert.Contains(marker, embed.Description);
        }

        [Fact]
        public async Task SayEmbed_MissingRequiredDescription_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}say embed <#{Fixture.ChannelId}> Title: no description here");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("description", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SayEmbed_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}say embed <#{Fixture.ChannelId}> Description: hi");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Read_MessageWithContent_ReturnsAttachment()
        {
            var content = "read test " + Guid.NewGuid().ToString("N")[..8];
            var message = await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, content);

            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}read {message.Id}");
            Assert.NotEmpty(result.Attachments);
        }

        [Fact]
        public async Task Read_InvokedByNonManager_Fails()
        {
            var message = await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, "read test " + Guid.NewGuid().ToString("N")[..8]);

            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}read {message.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReadEmbed_MessageWithEmbed_ReturnsAttachment()
        {
            var message = await Fixture.Admin.SendEmbedAsync(Fixture.ChannelId, new LocalEmbed().WithDescription("read embed test " + Guid.NewGuid().ToString("N")[..8]));

            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}read embed {message.Id}");
            Assert.NotEmpty(result.Attachments);
        }

        [Fact]
        public async Task ReadEmbed_MessageWithoutEmbed_Fails()
        {
            var message = await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, "no embed here " + Guid.NewGuid().ToString("N")[..8]);

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}read embed {message.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("embed", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Edit_MessageSentBySay_UpdatesContent()
        {
            var original = "edit-me " + Guid.NewGuid().ToString("N")[..8];
            var sent = await RunAdminCommandAsync($"{Options.CommandPrefix}say <#{Fixture.ChannelId}> {original}");
            Assert.Equal(original, sent.Content);

            var updated = "edited " + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}edit {sent.Id} {updated}");
            ResponseAssert.IsSuccess(ack);

            var refetched = await Fixture.Admin.Client.FetchMessageAsync(Fixture.ChannelId, sent.Id);
            Assert.Equal(updated, refetched!.Content);
        }

        [Fact]
        public async Task EditEmbed_MessageSentBySayEmbed_UpdatesEmbed()
        {
            var marker = Guid.NewGuid().ToString("N")[..8];
            var before = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}say embed <#{Fixture.ChannelId}> Description: original {marker}");
            var sent = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            Assert.Single(sent.Embeds);

            var updatedMarker = Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}edit embed {sent.Id} Description: updated {updatedMarker}");
            ResponseAssert.IsSuccess(ack);

            var refetched = (IUserMessage)(await Fixture.Admin.Client.FetchMessageAsync(Fixture.ChannelId, sent.Id))!;
            var embed = Assert.Single(refetched.Embeds);
            Assert.Contains(updatedMarker, embed.Description);
        }

        [Fact]
        public async Task Edit_InvokedByNonManager_Fails()
        {
            var sent = await RunAdminCommandAsync($"{Options.CommandPrefix}say <#{Fixture.ChannelId}> {"edit-me " + Guid.NewGuid().ToString("N")[..8]}");

            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}edit {sent.Id} nope");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Roles_ListsGuildRolesForAnyUser()
        {
            var before = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles");
            var result = await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            ResponseAssert.Contains(result, "everyone");
        }

        [Fact]
        public async Task Autoban_EnableThenDisable_Succeeds()
        {
            var regex = "test-" + Guid.NewGuid().ToString("N");
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}autoban <#{Fixture.ChannelId}> {regex}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autoban disable"));
            }
        }

        [Fact]
        public async Task Autoban_TargetingChannelWithoutSendPermission_Fails()
        {
            var regex = "test-" + Guid.NewGuid().ToString("N");
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}autoban <#{Fixture.NoSendChannelId}> {regex}");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Autoban_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autoban <#{Fixture.ChannelId}> test-{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DisableAutoban_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autoban disable");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ModDm_InSmallGuild_Fails()
        {
            // The command checks the guild's member count (must be 100+) before ever attempting a DM - the test
            // guild is always well under that, so this is a deterministic failure that doesn't depend on the
            // bot-to-bot DM restriction documented in README.md.
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}moddm <@{lowPrivId}> hello");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("100 members", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ModDm_InvokedByNonAdmin_Fails()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}moddm <@{lowPrivId}> hello");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Bans the low-privilege tester bot for real, confirms the ban, unbans it via a direct REST call (no
        /// unban command exists), then posts a re-invite link and polls membership via REST until a human
        /// re-invites it - banning always removes guild membership regardless of how quickly it's undone.
        /// </summary>
        [Fact]
        public async Task Ban_RealBan_RemovesMembershipAndCanBeUndoneByUnbanning()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

            try
            {
                await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
            }
            catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
            {
                Assert.Fail(
                    "The low-privilege tester bot isn't currently in the test guild. Re-invite it using the link " +
                    "this test posted the last time it ran, then run it again.");
                return;
            }

            // A bot's application (client) ID is its user ID, so the re-invite link can be built directly from
            // the bot's own identity instead of requiring a separate config value.
            var inviteUrl = $"https://discord.com/oauth2/authorize?client_id={lowPrivId}&permissions=0&scope=bot" +
                $"&guild_id={Options.GuildId}&disable_guild_select=true";

            await Fixture.LowPriv.SendMessageAsync(
                Fixture.ChannelId,
                "This bot is about to be banned (and immediately unbanned again) to test the real \"ban\" " +
                $"command. Please re-invite it within {Options.RejoinTimeout.TotalMinutes:0} minutes using this " +
                $"link: {inviteUrl}");

            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}ban integration-test-ban <@{lowPrivId}>");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("banned", ack.Content, StringComparison.OrdinalIgnoreCase);

                var ban = await Fixture.Admin.Client.FetchBanAsync(Options.GuildId, lowPrivId);
                Assert.NotNull(ban);
            }
            finally
            {
                await Fixture.Admin.Client.DeleteBanAsync(Options.GuildId, lowPrivId);
            }

            await WaitForRejoinAsync(lowPrivId, Options.RejoinTimeout);
        }

        private async Task WaitForRejoinAsync(Snowflake userId, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, userId);
                    return;
                }
                catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            Assert.Fail($"Timed out waiting for the low-privilege tester bot to be re-invited within {timeout.TotalMinutes:0} minutes.");
        }

        [Fact]
        public async Task Mute_Unmute_TogglesLowPrivSendPermissionInRunChannel()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            try
            {
                var muteAck = await RunAdminCommandAsync($"{Options.CommandPrefix}mute <@{lowPrivId}> testing");
                ResponseAssert.IsSuccess(muteAck);
                Assert.Contains("muted", muteAck.Content, StringComparison.OrdinalIgnoreCase);

                await AssertLowPrivCannotSendAsync();
            }
            finally
            {
                var unmuteAck = await RunAdminCommandAsync($"{Options.CommandPrefix}unmute <@{lowPrivId}>");
                ResponseAssert.IsSuccess(unmuteAck);
            }

            await AssertLowPrivCanSendAsync();
        }

        [Fact]
        public async Task Mute_InvokedByNonManager_Fails()
        {
            var adminId = Fixture.Admin.Client.CurrentUser!.Id;
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}mute <@{adminId}> testing");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Unmute_InvokedByNonManager_Fails()
        {
            var adminId = Fixture.Admin.Client.CurrentUser!.Id;
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}unmute <@{adminId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The "Muted" role's channel overwrite is applied asynchronously across every channel when the role is
        /// (re)created/assigned - retry rather than asserting on the first attempt.
        /// </summary>
        private async Task AssertLowPrivCannotSendAsync()
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, "mute test - should be blocked");
                }
                catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.Forbidden)
                {
                    return;
                }
            }

            Assert.Fail("Expected the muted low-privilege tester bot to be denied from sending messages in the run channel.");
        }

        private async Task AssertLowPrivCanSendAsync()
        {
            Exception? last = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, "mute test - should be allowed again");
                    return;
                }
                catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.Forbidden)
                {
                    last = ex;
                }
            }

            throw new TimeoutException("Expected the unmuted low-privilege tester bot to regain send permission in the run channel.", last);
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
