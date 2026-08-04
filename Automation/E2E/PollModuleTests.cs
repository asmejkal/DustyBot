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
    /// Command matrix for PollModule ("poll"/"vote" group, old stack - not yet migrated). Acks are correlated by
    /// timestamp, not reply, since the old framework never reply-links.
    ///
    /// Only one poll runs per channel at a time; since tests run sequentially and each ends its own poll in a
    /// `finally`, most run directly in the shared run channel. Two need a channel of their own: the
    /// "different channel" ack, and the "bot can't delete messages here" rejection (needs a specific permission
    /// override the shared channel can't carry).
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class PollModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _output;

        public PollModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task StartPoll_InSameChannel_PostsEmbedWithoutAck()
        {
            try
            {
                var result = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Is hotdog a sandwich?\" Yes No");
                var embed = Assert.Single(result.Embeds);
                Assert.Equal("Is hotdog a sandwich?", embed.Title);
                Assert.Contains("[1]", embed.Description);
                Assert.Contains("Yes", embed.Description);
                Assert.Contains("[2]", embed.Description);
                Assert.Contains("No", embed.Description);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task StartPoll_TargetingDifferentChannel_AcksAndPostsEmbed()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "poll-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll <#{channel.Id}> \"Q\" A1 A2");

                var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("poll started", ack.Content, StringComparison.OrdinalIgnoreCase);

                var posted = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, AckTimeout);
                Assert.Single(posted.Embeds);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end <#{channel.Id}>");
            }
        }

        [Fact]
        public async Task StartPoll_DuplicateChannel_Fails()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");

                var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Another Q\" A1 A2");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("already a poll running", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task StartPoll_TargetingChannelWithoutSendPermission_Fails()
        {
            var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll <#{Fixture.NoSendChannelId}> \"Q\" A1 A2");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't send messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StartPoll_AnonymousTargetingChannelWithoutManageMessages_Fails()
        {
            var channel = await Fixture.Admin.Client.CreateTextChannelAsync(Options.GuildId, "poll-nomanage-" + Guid.NewGuid().ToString("N")[..8], x =>
                x.Overwrites = new[]
                {
                    new LocalOverwrite(Options.TargetBotId, OverwriteTargetType.Member, new OverwritePermissions(Permissions.None, Permissions.ManageMessages)),
                });
            try
            {
                var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll anonymous <#{channel.Id}> \"Q\" A1 A2");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("can't delete messages", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.Client.DeleteChannelAsync(channel.Id);
            }
        }

        [Fact]
        public async Task StartPoll_InvokedByNonManager_Fails()
        {
            var before = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");
            var ack = await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StartPoll_TooManyAnswers_Fails()
        {
            // 900 single-character answers push the rendered embed description well past its 4096-char limit,
            // while the raw command text (~1800 chars) still comfortably fits Discord's 2000-char message limit.
            var manyAnswers = string.Join(" ", Enumerable.Repeat("a", 900));
            var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" a a {manyAnswers}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("too long", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EndPoll_ClosesPoll_ThenSubsequentEndFails()
        {
            await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");

            var closeResult = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            var embed = Assert.Single(closeResult.Embeds);
            Assert.Equal("Poll closed!", embed.Title);

            var reEndAck = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            ResponseAssert.IsFailure(reEndAck);
            Assert.Contains("no poll is currently running", reEndAck.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EndPoll_InvokedByNonManager_Fails()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");

                var before = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
                var ack = await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task ResultsPoll_NonAnonymousPoll_AnyoneCanView()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");

                var before = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll results");
                var result = await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                var embed = Assert.Single(result.Embeds);
                Assert.Equal("Poll results", embed.Title);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task ResultsPoll_AnonymousPoll_RequiresManageMessages()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll anonymous \"Q\" A1 A2");

                var before = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll results");
                var lowPrivAck = await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                ResponseAssert.IsFailure(lowPrivAck);
                Assert.Contains("anonymous polls can only be viewed by moderators", lowPrivAck.Content, StringComparison.OrdinalIgnoreCase);

                var adminResult = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll results");
                Assert.Single(adminResult.Embeds);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task ResultsPoll_NoPollRunning_Fails()
        {
            var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll results");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no poll is currently running", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Vote_ByNumber_Succeeds()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" Pineapple Mushroom");

                var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote 1");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("vote cast", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task Vote_ByAnswerText_Succeeds()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" Pineapple Mushroom");

                var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote pineapple");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("vote cast", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task Vote_WithAmbiguousOrUnrecognizedText_AsksForClarification()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" \"Red Apple\" \"Red Banana\"");

                var ambiguousAck = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote red");
                Assert.Contains("not sure which answer", ambiguousAck.Content, StringComparison.OrdinalIgnoreCase);

                var unrecognizedAck = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote zzznomatch");
                Assert.Contains("don't recognize this answer", unrecognizedAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task Vote_InvalidNumber_Fails()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");

                var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote 99");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("no answer with this number", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task Vote_NoPollRunning_Fails()
        {
            var ack = await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote 1");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no poll running in this channel", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Vote_OnAnonymousPoll_DeletesVoterMessageAndAutoDeletesConfirmation()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll anonymous \"Q\" A1 A2");

                var before = DateTimeOffset.UtcNow;
                var voteMessage = await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote 1");
                var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                ResponseAssert.IsSuccess(ack);

                Assert.Null(await TryFetchMessageAsync(Fixture.ChannelId, voteMessage.Id));

                // The confirmation ack is scheduled to self-delete after 2 seconds (DeleteAfter(2)) - this is a
                // known, deterministic timer, not gateway-cache-propagation lag, so outlasting it with a fixed
                // wait is correct here rather than a "retry, don't blind-sleep" violation.
                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.Null(await TryFetchMessageAsync(Fixture.ChannelId, ack.Id));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
        }

        [Fact]
        public async Task Vote_OnNonAnonymousPoll_DoesNotDeleteMessages()
        {
            try
            {
                await RunAdminCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll \"Q\" A1 A2");

                var before = DateTimeOffset.UtcNow;
                var voteMessage = await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}vote 1");
                var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                ResponseAssert.IsSuccess(ack);

                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.NotNull(await TryFetchMessageAsync(Fixture.ChannelId, voteMessage.Id));
                Assert.NotNull(await TryFetchMessageAsync(Fixture.ChannelId, ack.Id));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}poll end");
            }
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

        private Task<IGatewayUserMessage> RunAdminCommandAsync(Snowflake channelId, string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, channelId, commandText, AckTimeout);
    }
}
