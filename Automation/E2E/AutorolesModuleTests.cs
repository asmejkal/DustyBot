using System;
using System.Linq;
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
    /// Command matrix for AutorolesModule ("autorole" group, old stack - not yet migrated). Acks are correlated
    /// by timestamp, not reply, since the old framework never reply-links.
    ///
    /// "apply"/"check" self-lock the entire guild for 1h/30min after completing (a hand-rolled limit, not the
    /// framework's - see WRITING_TESTS.md), so each is invoked for real exactly once per suite run, verifying
    /// the resulting lockout on an immediate second call. The configured autorole list is shared guild state
    /// with no bulk-clear command, so the first test to run resets it via RunOnceGate.
    /// RealMemberRejoin_AutoAssignsConfiguredRole needs a real leave/rejoin cycle and a human to click the
    /// re-invite link it posts.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class AutorolesModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan LongRunningTimeout = TimeSpan.FromSeconds(30);
        private static readonly RunOnceGate ResetOnce = new();

        private readonly ITestOutputHelper _output;

        public AutorolesModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
            await ResetOnce.RunOnceAsync(async () =>
            {
                var listing = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole list");
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(listing.Content ?? string.Empty, @"Id: `(\d+)`"))
                    await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {match.Groups[1].Value}");
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task AddAutoRole_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-add-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("assign role", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {role.Id}");
            }
        }

        [Fact]
        public async Task AddAutoRole_InvokedByNonAdmin_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-add-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RemoveAutoRole_ConfiguredRole_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-remove-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole remove {role.Id}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task RemoveAutoRole_NotConfigured_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-notset-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole remove {role.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("not being assigned", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RemoveAutoRole_InvokedByNonAdmin_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-remove-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autorole remove {role.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {role.Id}");
            }
        }

        [Fact]
        public async Task ListAutoRole_NoRolesConfigured_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole list");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task ListAutoRole_WithRoles_ListsThem()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-list-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}"));

                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole list");
                ResponseAssert.Contains(result, role.Id.ToString());
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {role.Id}");
            }
        }

        [Fact]
        public async Task ListAutoRole_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autorole list");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyAutoRole_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autorole apply");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Combines the "nothing configured" check, the real assignment effect, and the post-completion rate
        /// limit into one test, since "apply" locks the guild out for an hour after it actually runs - see the
        /// class doc comment. Do not add another test that invokes "autorole apply" for real.
        /// </summary>
        [Fact]
        public async Task ApplyAutoRole_AssignsRoleToAllMembers_ThenRateLimitsASecondCall()
        {
            var emptyAck = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole apply");
            ResponseAssert.IsFailure(emptyAck);

            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-apply-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}"));

                var result = await RunAdminLongRunningCommandAsync($"{Options.CommandPrefix}autorole apply", LongRunningTimeout);
                ResponseAssert.IsSuccess(result);

                var admin = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, Fixture.Admin.Client.CurrentUser!.Id);
                var lowPriv = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, Fixture.LowPriv.Client.CurrentUser!.Id);
                Assert.Contains(role.Id, admin!.RoleIds);
                Assert.Contains(role.Id, lowPriv!.RoleIds);

                var rateLimited = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole apply");
                Assert.Contains("wait", rateLimited.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {role.Id}");
            }
        }

        [Fact]
        public async Task CheckAutoRole_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}autorole check");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Same combining rationale as ApplyAutoRole's test, but for "check"'s 30-minute lockout. Uses its own
        /// role (never assigned via "apply") so it reports members missing it, rather than piggybacking on
        /// ApplyAutoRole's test and asserting "everyone already has it".
        /// </summary>
        [Fact]
        public async Task CheckAutoRole_ReportsMembersMissingRole_ThenRateLimitsASecondCall()
        {
            var emptyAck = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole check");
            ResponseAssert.IsFailure(emptyAck);

            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-check-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}"));

                var result = await RunAdminLongRunningCommandAsync($"{Options.CommandPrefix}autorole check", LongRunningTimeout);
                Assert.Contains("missing", result.Content, StringComparison.OrdinalIgnoreCase);

                var rateLimited = await RunAdminCommandAsync($"{Options.CommandPrefix}autorole check");
                Assert.Contains("wait", rateLimited.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {role.Id}");
            }
        }

        /// <summary>
        /// Configures its own role (never touched by the apply/check tests, so it doesn't interact with their
        /// guild-wide lockouts), has the low-privilege tester bot actually leave and wait for a human to
        /// re-invite it (there's no programmatic way to make a bot rejoin), then confirms the join auto-assigned
        /// the configured role - the only way to exercise HandleUserJoined for real.
        /// </summary>
        [Fact]
        public async Task RealMemberRejoin_AutoAssignsConfiguredRole()
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

            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "autorole-join-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}autorole add {role.Id}"));

                // A bot's application (client) ID is its user ID, so the re-invite link can be built directly
                // from the bot's own identity instead of requiring a separate config value.
                var inviteUrl = $"https://discord.com/oauth2/authorize?client_id={lowPrivId}&permissions=0&scope=bot" +
                    $"&guild_id={Options.GuildId}&disable_guild_select=true";

                await Fixture.LowPriv.SendMessageAsync(
                    Fixture.ChannelId,
                    "This bot is about to leave the server to test the real autorole join trigger. Please " +
                    $"re-invite it within {Options.RejoinTimeout.TotalMinutes:0} minutes using this link: {inviteUrl}");

                await Fixture.LowPriv.Client.LeaveGuildAsync(Options.GuildId);

                await WaitForRejoinAsync(lowPrivId, Options.RejoinTimeout);

                IMember? member = null;
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    member = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
                    if (member!.RoleIds.Contains(role.Id))
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                Assert.Contains(role.Id, member!.RoleIds);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}autorole remove {role.Id}");
            }
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

        /// <summary>"apply"/"check" send an initial progress message, then edit (not resend) it, before deleting
        /// it and sending the real final message - skip past that progress message rather than treating it as
        /// the answer.</summary>
        private async Task<IGatewayUserMessage> RunAdminLongRunningCommandAsync(string commandText, TimeSpan timeout)
        {
            var cursor = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, commandText);

            var deadline = DateTimeOffset.UtcNow + timeout;
            while (true)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException($"Timed out after {timeout} waiting for a final (non-progress) response to \"{commandText}\".");

                var message = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, cursor, remaining);
                if (message.Content?.Contains("This may take a while", StringComparison.OrdinalIgnoreCase) != true)
                    return message;

                cursor = message.Id.CreatedAt.AddMilliseconds(1);
            }
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
