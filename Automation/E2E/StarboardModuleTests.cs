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
    /// Command matrix for StarboardModule ("starboard" group, old stack - not yet migrated). Acks are
    /// correlated by timestamp, not reply, since the old framework never reply-links.
    ///
    /// The bot-whitelist gap here is on the *starred message's author*, not the acting user:
    /// ProcessNewStar/ProcessRemovedStar unconditionally skip any message authored by a bot. `IsBlockedBotAuthor`
    /// allows whitelisted bot authors through (reusing BotIntegrationOptions) so tester-bot messages can be
    /// starred for real. A fresh starboard defaults to threshold 1 and emoji ⭐, so a single real star is enough
    /// to trigger a repost with no extra setup.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class StarboardModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RealEffectTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan NoRepostTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan RetainedRepostSettleDelay = TimeSpan.FromSeconds(8);
        private static readonly LocalEmoji StarEmoji = new("⭐");

        private readonly ITestOutputHelper _output;

        public StarboardModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task AddStarboard_Succeeds()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard add <#{channel.Id}>");
            ResponseAssert.IsSuccess(ack);
            var id = ExtractStarboardId(ack.Content);

            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
        }

        [Fact]
        public async Task AddStarboard_TargetingChannelWithoutSendPermission_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard add <#{Fixture.NoSendChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't send messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddStarboard_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}starboard add <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetStyle_Succeeds()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var textAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set style {id} text");
                ResponseAssert.IsSuccess(textAck);
                Assert.Contains("text", textAck.Content, StringComparison.OrdinalIgnoreCase);

                var embedAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set style {id} embed");
                ResponseAssert.IsSuccess(embedAck);
                Assert.Contains("embed", embedAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetStyle_InvalidStyle_Fails()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set style {id} notastyle");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("unknown style", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetStyle_NonexistentId_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set style 999999 embed");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no starboard found", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetEmojis_Succeeds()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set emoji {id} \U0001F31F");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetThreshold_Succeeds()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set threshold {id} 3");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("3", ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetThreshold_ZeroValue_Fails()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set threshold {id} 0");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("out of allowed range", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetChannelsWhitelist_SetsThenClears()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var setAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set channels {id} <#{Fixture.ChannelId}>");
                ResponseAssert.IsSuccess(setAck);

                var clearAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set channels {id}");
                ResponseAssert.IsSuccess(clearAck);
                Assert.Contains("all channels", clearAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetRule_AllRuleTypes_Succeed()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var selfStarsAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} SelfStars yes");
                ResponseAssert.IsSuccess(selfStarsAck);

                var keepUnstarredAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} KeepUnstarred yes");
                ResponseAssert.IsSuccess(keepUnstarredAck);

                var keepDeletedAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} KeepDeleted yes");
                ResponseAssert.IsSuccess(keepDeletedAck);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task SetRule_UnknownRule_Fails()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} NotARule yes");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("unknown rule", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task ListStarboards_Empty_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard list");
            Assert.Contains("no starboards have been set up", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ListStarboards_WithStarboards_ListsThem()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard list");
                Assert.Contains($"ID: `{id}`", result.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RemoveStarboard_Succeeds()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard remove {id}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task RemoveStarboard_NonexistentId_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard remove 999999");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no starboard found", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StarboardRanking_WithNoStars_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard ranking");
            Assert.Contains("no users with starred messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StarboardTop_WithNoStars_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard top");
            Assert.Contains("no starred messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Uses a real bot-sent message (this test's own "starboard list" ack) rather than an arbitrary ID -
        /// GuildSelfMessage requires the ID to resolve to an actual message the bot sent, so a non-message ID
        /// would fail at parameter resolution instead of reaching RemoveStarboardMessage's own "not tracked as
        /// a starboard repost" check, which is what this test means to exercise.
        /// </summary>
        [Fact]
        public async Task RemoveStarboardMessage_NotFound_Fails()
        {
            var listAck = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard list");
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard remove message {listAck.Id}");
            Assert.Contains("can't find message", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A fresh starboard defaults to threshold 1 and the ⭐ emoji, so one real star from Admin on a message
        /// LowPriv posted is enough to trigger a real repost. Also exercises "starboard ranking"/"top" against
        /// that real data, and LowPriv removing their own starred repost via "starboard remove message".
        /// </summary>
        [Fact]
        public async Task RealStarboard_RepostsMessage_ThenUpdatesRankingAndTop_ThenRemovedByAuthor()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var content = "starboard-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);

                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);
                var embed = Assert.Single(repost.Embeds);
                Assert.Contains(content, embed.Description);

                var rankingResult = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard ranking");
                ResponseAssert.Contains(rankingResult, Fixture.LowPriv.Client.CurrentUser!.Name);

                var topResult = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard top");
                ResponseAssert.Contains(topResult, content);

                var removeAck = await RunLowPrivCommandAsync($"{Options.CommandPrefix}starboard remove message {repost.Id}");
                ResponseAssert.Contains(removeAck, "removed your starred message");

                Assert.Null(await TryFetchMessageAsync(channel.Id, repost.Id));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_UnstarringBelowThreshold_DeletesRepost()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var content = "unstar-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);
                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);

                await Fixture.Admin.Client.RemoveOwnReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);

                await AssertEventuallyDeletedAsync(channel.Id, repost.Id);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_SourceMessageDeleted_RemovesRepost()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                var content = "sourcedelete-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);
                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);

                await sourceMessage.DeleteAsync();

                await AssertEventuallyDeletedAsync(channel.Id, repost.Id);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_TextStyle_RepostsAsPlainMessage()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set style {id} text"));
            try
            {
                var content = "textstyle-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);

                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);
                Assert.Empty(repost.Embeds);
                Assert.Contains(content, repost.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_AllowSelfStars_AuthorCanStarOwnMessage()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} SelfStars yes"));

                var content = "selfstar-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);

                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);
                var embed = Assert.Single(repost.Embeds);
                Assert.Contains(content, embed.Description);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_ChannelsWhitelist_OnlyWhitelistedChannelReposts()
        {
            await using var starChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            await using var whitelistedChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "starsrc-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(starChannel.Id);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set channels {id} <#{whitelistedChannel.Id}>"));

                var nonWhitelistedContent = "whitelist-neg-" + Guid.NewGuid().ToString("N")[..8];
                var nonWhitelistedMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, nonWhitelistedContent);
                var beforeNonWhitelisted = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, nonWhitelistedMessage.Id, StarEmoji);

                await Assert.ThrowsAsync<TimeoutException>(() =>
                    Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, starChannel.Id, beforeNonWhitelisted, NoRepostTimeout));

                var whitelistedContent = "whitelist-pos-" + Guid.NewGuid().ToString("N")[..8];
                var whitelistedMessage = await Fixture.LowPriv.SendMessageAsync(whitelistedChannel.Id, whitelistedContent);
                var beforeWhitelisted = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(whitelistedChannel.Id, whitelistedMessage.Id, StarEmoji);

                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, starChannel.Id, beforeWhitelisted, RealEffectTimeout);
                var embed = Assert.Single(repost.Embeds);
                Assert.Contains(whitelistedContent, embed.Description);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_KeepUnstarred_RetainsRepostBelowThreshold()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} KeepUnstarred yes"));

                var content = "keepunstarred-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);
                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);

                await Fixture.Admin.Client.RemoveOwnReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);

                var embed = await AssertEventuallyHasFooterAsync(channel.Id, repost.Id, "⭐ 0");
                Assert.Contains(content, embed.Description);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        [Fact]
        public async Task RealStarboard_KeepDeleted_RetainsRepostAfterSourceDeletion()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "star-" + Guid.NewGuid().ToString("N")[..8]);
            var id = await CreateStarboardAsync(channel.Id);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}starboard set rule {id} KeepDeleted yes"));

                var content = "keepdeleted-test-" + Guid.NewGuid().ToString("N")[..8];
                var sourceMessage = await Fixture.LowPriv.SendMessageAsync(Fixture.ChannelId, content);

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.Client.AddReactionAsync(Fixture.ChannelId, sourceMessage.Id, StarEmoji);
                var repost = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, RealEffectTimeout);

                await sourceMessage.DeleteAsync();

                await Task.Delay(RetainedRepostSettleDelay);
                Assert.NotNull(await TryFetchMessageAsync(channel.Id, repost.Id));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}starboard remove {id}");
            }
        }

        private async Task<int> CreateStarboardAsync(Snowflake channelId)
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}starboard add <#{channelId}>");
            ResponseAssert.IsSuccess(ack);
            return ExtractStarboardId(ack.Content);
        }

        private static int ExtractStarboardId(string content)
        {
            var match = System.Text.RegularExpressions.Regex.Match(content, @"[Ss]tarboard `(\d+)`");
            Assert.True(match.Success, $"Could not find a starboard ID in: \"{content}\"");
            return int.Parse(match.Groups[1].Value);
        }

        private async Task AssertEventuallyDeletedAsync(Snowflake channelId, Snowflake messageId)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (await TryFetchMessageAsync(channelId, messageId) == null)
                    return;

                await Task.Delay(TimeSpan.FromSeconds(1.5));
            }

            Assert.Fail($"Expected message {messageId} in channel {channelId} to eventually be deleted.");
        }

        private async Task<IEmbed> AssertEventuallyHasFooterAsync(Snowflake channelId, Snowflake messageId, string expectedFooterSubstring)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var message = await Fixture.Admin.Client.FetchMessageAsync(channelId, messageId) as IUserMessage;
                Assert.NotNull(message);

                var embed = message!.Embeds.SingleOrDefault();
                if (embed?.Footer?.Text?.Contains(expectedFooterSubstring, StringComparison.Ordinal) == true)
                    return embed;

                await Task.Delay(TimeSpan.FromSeconds(1.5));
            }

            Assert.Fail($"Expected message {messageId} in channel {channelId} to eventually show a footer containing \"{expectedFooterSubstring}\".");
            return null!;
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
