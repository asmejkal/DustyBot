using System;
using System.Collections.Generic;
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
    /// Command matrix for RaidProtectionModule ("raid protection" group, old stack - not yet migrated). Acks are
    /// correlated by timestamp, not reply, since the old framework never reply-links.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class RaidProtectionModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan EnforcementTimeout = TimeSpan.FromSeconds(20);
        private static readonly RunOnceGate SetupOnce = new();

        private readonly ITestOutputHelper _output;

        public RaidProtectionModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
            await SetupOnce.RunOnceAsync(async () =>
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection enable <#{Fixture.ChannelId}>");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection blacklist clear");
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task EnableRaidProtection_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection enable <#{Fixture.ChannelId}>");
            ResponseAssert.IsSuccess(ack);
            Assert.Contains("enabled", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EnableRaidProtection_TargetingChannelWithoutSendPermission_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection enable <#{Fixture.NoSendChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't send messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EnableRaidProtection_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection enable <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DisableThenReEnable_PreservesBlacklist()
        {
            var phrase = "keep" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add {phrase}"));

                var disableAck = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection disable");
                ResponseAssert.IsSuccess(disableAck);

                var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist list");
                ResponseAssert.Contains(listResult, phrase);
            }
            finally
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection enable <#{Fixture.ChannelId}>"));
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection blacklist remove {phrase}");
            }
        }

        [Fact]
        public async Task DisableRaidProtection_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection disable");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ListRules_ShowsStatusAndRuleTypes_Succeeds()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection rules");
            Assert.Contains("enabled", result.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("MassMentionsRule", result.Content);
            Assert.Contains("TextSpamRule", result.Content);
            Assert.Contains("ImageSpamRule", result.Content);
            Assert.Contains("PhraseBlacklistRule", result.Content);
        }

        [Fact]
        public async Task ListRules_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection rules");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetMaxOffenseCount_Succeeds()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection set max offenses MassMentionsRule 3");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("MassMentionsRule", ack.Content);
                Assert.Contains("3", ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection set max offenses MassMentionsRule 2");
            }
        }

        [Fact]
        public async Task SetMaxOffenseCount_InvalidRuleName_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection set max offenses NotARealRule 3");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("unknown rule type", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetMaxOffenseCount_ValueLessThanOne_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection set max offenses MassMentionsRule 0");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("at least 1", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetMaxOffenseCount_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection set max offenses MassMentionsRule 3");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BlacklistAdd_Succeeds_AndIsListed()
        {
            var phrase = "add" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add {phrase}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(phrase, ack.Content);

                var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist list");
                ResponseAssert.Contains(listResult, phrase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection blacklist remove {phrase}");
            }
        }

        [Fact]
        public async Task BlacklistAdd_PhraseTooShort_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add ab");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("at least 3 characters", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BlacklistAdd_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection blacklist add nonmanager{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BlacklistRemove_Succeeds()
        {
            var phrase = "rem" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add {phrase}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist remove {phrase}");
            ResponseAssert.IsSuccess(ack);

            var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist list");
            ResponseAssert.DoesNotContain(listResult, phrase);
        }

        [Fact]
        public async Task BlacklistRemove_NotInBlacklist_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist remove notpresent{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("are not in the blacklist", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BlacklistRemove_InvokedByNonManager_Fails()
        {
            var phrase = "rem" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add {phrase}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection blacklist remove {phrase}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection blacklist remove {phrase}");
            }
        }

        [Fact]
        public async Task BlacklistClear_Succeeds()
        {
            var phrase = "clr" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add {phrase}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist clear");
            ResponseAssert.IsSuccess(ack);

            var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist list");
            ResponseAssert.IsFailure(listResult);
        }

        [Fact]
        public async Task BlacklistList_Empty_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist list");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no blacklisted phrases", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BlacklistList_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}raid protection blacklist list");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RulesSet_InvokedByNonOwner_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection rules set {Options.GuildId} MassMentionsRule 10;2;5");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("bot owner", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RulesReset_InvokedByNonOwner_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection rules reset {Options.GuildId} MassMentionsRule");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("bot owner", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Bursts 6 plain messages (TextSpamRule's default threshold) within its 3-second window via the raw
        /// Disqord client, bypassing TestDiscordClient's send pacing - safe here since the rate limit only
        /// applies to command processing, not this module's raw message listener. MaxOffenseCount defaults to
        /// 2, so a single burst is only a warning, not a mute.
        /// </summary>
        [Fact]
        public async Task RealTextSpam_DeletesMessagesAndWarnsOnFirstOffense()
        {
            var before = DateTimeOffset.UtcNow;
            var sent = await BurstSendAsync(Fixture.LowPriv, Fixture.ChannelId, 6, "spam-warn-test");

            var (content, embeds) = await CollectContentAsync(before, 2, EnforcementTimeout);
            Assert.Contains("broken a raid protection rule", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(embeds, e => e.Description?.Contains("Warned user", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var message in sent)
                await AssertEventuallyDeletedAsync(Fixture.ChannelId, message.Id);
        }

        /// <summary>
        /// Temporarily drops TextSpamRule's MaxOffenseCount to 1 so a single burst immediately escalates to a
        /// mute instead of a warning, reusing the same real mute mechanism AdministrationModuleTests exercises
        /// directly. Always unmutes LowPriv in `finally` - a stuck mute would break every other test in the
        /// entire suite that needs LowPriv to send messages.
        /// </summary>
        [Fact]
        public async Task RealTextSpam_MutesAfterOffenseLimitReached()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection set max offenses TextSpamRule 1"));
            try
            {
                var before = DateTimeOffset.UtcNow;
                var sent = await BurstSendAsync(Fixture.LowPriv, Fixture.ChannelId, 6, "spam-mute-test");

                var (content, _) = await CollectContentAsync(before, 2, EnforcementTimeout);
                Assert.Contains("you have been muted", content, StringComparison.OrdinalIgnoreCase);

                foreach (var message in sent)
                    await AssertEventuallyDeletedAsync(Fixture.ChannelId, message.Id);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}unmute <@{lowPrivId}>");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection set max offenses TextSpamRule 2");
            }
        }

        [Fact]
        public async Task RealPhraseBlacklist_DeletesMessageAndWarns()
        {
            var phrase = "blk" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}raid protection blacklist add {phrase}"));

                var before = DateTimeOffset.UtcNow;
                var sent = (await Fixture.LowPriv.Client.SendMessageAsync(Fixture.ChannelId, new LocalMessage().WithContent($"this message contains {phrase} the blacklisted phrase")))!;

                var (content, embeds) = await CollectContentAsync(before, 2, EnforcementTimeout);
                Assert.Contains("broken a raid protection rule", content, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(embeds, e => e.Description?.Contains("Warned user", StringComparison.OrdinalIgnoreCase) == true);

                await AssertEventuallyDeletedAsync(Fixture.ChannelId, sent.Id);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}raid protection blacklist remove {phrase}");
            }
        }

        private async Task<List<IUserMessage>> BurstSendAsync(TestDiscordClient client, Snowflake channelId, int count, string contentPrefix)
        {
            var messages = new List<IUserMessage>();
            for (var i = 0; i < count; i++)
                messages.Add((await client.Client.SendMessageAsync(channelId, new LocalMessage().WithContent($"{contentPrefix} {Guid.NewGuid():N} {i}")))!);

            return messages;
        }

        /// <summary>
        /// EnforceRule sends a warning/mute message and a log embed as two separate messages with no guaranteed
        /// order between them - collects both and returns their combined text/embeds rather than assuming which
        /// arrives first.
        /// </summary>
        private async Task<(string Content, IEnumerable<IEmbed> Embeds)> CollectContentAsync(DateTimeOffset after, int count, TimeSpan timeout)
        {
            var cursor = after;
            var contents = new List<string>();
            var embeds = new List<IEmbed>();
            for (var i = 0; i < count; i++)
            {
                var message = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, cursor, timeout);
                if (!string.IsNullOrEmpty(message.Content))
                    contents.Add(message.Content);

                embeds.AddRange(message.Embeds);
                cursor = message.Id.CreatedAt.AddMilliseconds(1);
            }

            return (string.Join(" ", contents), embeds);
        }

        private async Task AssertEventuallyDeletedAsync(Snowflake channelId, Snowflake messageId)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (await TryFetchMessageAsync(channelId, messageId) == null)
                    return;

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Assert.Fail($"Expected message {messageId} in channel {channelId} to eventually be deleted.");
        }

        private async Task<IMessage?> TryFetchMessageAsync(Snowflake channelId, Snowflake messageId)
        {
            try
            {
                return await Fixture.Admin.Client.FetchMessageAsync(channelId, messageId);
            }
            catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
            {
                return null;
            }
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
