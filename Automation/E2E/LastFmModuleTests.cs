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
    /// Command matrix for LastFmModule ("lf"/"lastfm" group). Acks are correlated
    /// by timestamp, not reply, since the old framework never reply-links. No command here has a permission
    /// attribute, so there's no admin-vs-non-admin matrix.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class LastFmModuleTests : IAsyncLifetime
    {
        private const string RealLastFmUsername = "churchofbirch";

        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(30);
        private static readonly RunOnceGate ResetOnce = new();

        private readonly ITestOutputHelper _output;

        public LastFmModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}lf reset");
                await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}lf reset");
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task NowPlaying_WithoutOwnUsernameSet_Fails()
        {
            var ack = await RunCommandAsync($"{Options.CommandPrefix}lf np", AckTimeout);
            Assert.Contains("haven't set", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NowPlaying_WithOffDiscordUsername_Succeeds()
        {
            var result = await RunCommandAsync($"{Options.CommandPrefix}lf np {RealLastFmUsername}", NetworkTimeout);
            ResponseAssert.Contains(result, "listen");
        }

        [Fact]
        public async Task NowPlaying_WithUnknownLastFmUsername_Fails()
        {
            var ack = await RunCommandAsync($"{Options.CommandPrefix}lf np nonexistent-lastfm-user-{Guid.NewGuid():N}", NetworkTimeout);
            ResponseAssert.IsFailure(ack);
            Assert.Contains("not found", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NowPlayingSpotify_WithOffDiscordUsername_Succeeds()
        {
            var result = await RunCommandAsync($"{Options.CommandPrefix}lf np spotify {RealLastFmUsername}", NetworkTimeout);

            // Whether a Spotify match is found depends on the real account's most recent track at run time -
            // both outcomes prove the Last.fm -> Spotify search round trip completed without erroring.
            var content = result.Content ?? string.Empty;
            Assert.True(
                content.Contains("listening to", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("can't find this track", StringComparison.OrdinalIgnoreCase),
                $"Expected either a Spotify link or a \"can't find this track\" message. Content: \"{content}\"");
        }

        [Fact]
        public async Task LastFmRecent_WithOffDiscordUsername_Succeeds()
        {
            var result = await RunCommandAsync($"{Options.CommandPrefix}lf recent {RealLastFmUsername}", NetworkTimeout);
            Assert.Contains(result.Embeds, e => e.Author?.Name?.Contains("listened to", StringComparison.OrdinalIgnoreCase) == true);
        }

        [Fact]
        public async Task LastFmArtists_WithOffDiscordUsername_Succeeds()
        {
            var result = await RunCommandAsync($"{Options.CommandPrefix}lf top artists {RealLastFmUsername}", NetworkTimeout);
            Assert.Contains(result.Embeds, e => e.Author?.Name?.Contains("top artists", StringComparison.OrdinalIgnoreCase) == true);
        }

        [Fact]
        public async Task LastFmAlbums_WithOffDiscordUsername_Succeeds()
        {
            var result = await RunCommandAsync($"{Options.CommandPrefix}lf top albums {RealLastFmUsername}", NetworkTimeout);
            Assert.Contains(result.Embeds, e => e.Author?.Name?.Contains("top albums", StringComparison.OrdinalIgnoreCase) == true);
        }

        [Fact]
        public async Task LastFmTracks_WithOffDiscordUsername_Succeeds()
        {
            var result = await RunCommandAsync($"{Options.CommandPrefix}lf top tracks {RealLastFmUsername}", NetworkTimeout);
            Assert.Contains(result.Embeds, e => e.Author?.Name?.Contains("top tracks", StringComparison.OrdinalIgnoreCase) == true);
        }

        [Fact]
        public async Task LastFmSet_ThenReset_RoundTrips()
        {
            var fakeUsername = "fake-lastfm-user-" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                var setAck = await RunCommandAsync($"{Options.CommandPrefix}lf set {fakeUsername}", AckTimeout);
                ResponseAssert.IsSuccess(setAck);
                Assert.Contains(fakeUsername, setAck.Content);

                // Confirms "np" actually reads the just-set username back: a fake username reaches Last.fm's
                // real API and comes back "not found", rather than "you haven't set a username".
                var npAck = await RunCommandAsync($"{Options.CommandPrefix}lf np", NetworkTimeout);
                ResponseAssert.IsFailure(npAck);
                Assert.Contains("not found", npAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                ResponseAssert.IsSuccess(await RunCommandAsync($"{Options.CommandPrefix}lf reset", AckTimeout));
            }

            var afterResetAck = await RunCommandAsync($"{Options.CommandPrefix}lf np", AckTimeout);
            Assert.Contains("haven't set", afterResetAck.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LastFmSetAnonymous_HidesProfileLinkAndDeletesInvocation()
        {
            try
            {
                var before = DateTimeOffset.UtcNow;
                var sent = await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}lf set anonymous {RealLastFmUsername}");
                var setAck = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
                ResponseAssert.IsSuccess(setAck);
                Assert.DoesNotContain(RealLastFmUsername, setAck.Content);

                IMessage? refetched;
                try
                {
                    refetched = await Fixture.Admin.Client.FetchMessageAsync(Fixture.ChannelId, sent.Id);
                }
                catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
                {
                    refetched = null;
                }

                Assert.Null(refetched);

                var npResult = await RunCommandAsync($"{Options.CommandPrefix}lf np", NetworkTimeout);
                ResponseAssert.Contains(npResult, "listen");
                var embed = Assert.Single(npResult.Embeds);
                Assert.Null(embed.Author?.Url);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}lf reset");
            }
        }

        /// <summary>
        /// Sets Admin's own username to a real account and exercises every self-path command against it in one
        /// pass (the self branch of GetLastFmSettings, a real cross-account "lf compare" via `jesus`, and
        /// "lf artist" for a real ("ARTMS") and nonexistent artist), rather than a separate set/reset per
        /// command.
        /// </summary>
        [Fact]
        public async Task LastFmSet_WithRealAccount_SucceedsAcrossSelfPathCommandsAndCompare()
        {
            var setAck = await RunCommandAsync($"{Options.CommandPrefix}lf set {RealLastFmUsername}", AckTimeout);
            ResponseAssert.IsSuccess(setAck);
            Assert.Contains(RealLastFmUsername, setAck.Content);

            try
            {
                var npResult = await RunCommandAsync($"{Options.CommandPrefix}lf np", NetworkTimeout);
                ResponseAssert.Contains(npResult, "listen");

                var recentResult = await RunCommandAsync($"{Options.CommandPrefix}lf recent", NetworkTimeout);
                Assert.Contains(recentResult.Embeds, e => e.Author?.Name?.Contains("listened to", StringComparison.OrdinalIgnoreCase) == true);

                var artistsResult = await RunCommandAsync($"{Options.CommandPrefix}lf top artists", NetworkTimeout);
                Assert.Contains(artistsResult.Embeds, e => e.Author?.Name?.Contains("top artists", StringComparison.OrdinalIgnoreCase) == true);

                var albumsResult = await RunCommandAsync($"{Options.CommandPrefix}lf top albums", NetworkTimeout);
                Assert.Contains(albumsResult.Embeds, e => e.Author?.Name?.Contains("top albums", StringComparison.OrdinalIgnoreCase) == true);

                var tracksResult = await RunCommandAsync($"{Options.CommandPrefix}lf top tracks", NetworkTimeout);
                Assert.Contains(tracksResult.Embeds, e => e.Author?.Name?.Contains("top tracks", StringComparison.OrdinalIgnoreCase) == true);

                var artistResult = await RunCommandAsync($"{Options.CommandPrefix}lf artist ARTMS", NetworkTimeout);
                ResponseAssert.Contains(artistResult, "listened to this artist");

                var missingArtistAck = await RunCommandAsync($"{Options.CommandPrefix}lf artist nonexistent-artist-{Guid.NewGuid():N}", NetworkTimeout);
                ResponseAssert.IsFailure(missingArtistAck);
                Assert.Contains("can't find this artist", missingArtistAck.Content, StringComparison.OrdinalIgnoreCase);

                var compareResult = await RunCommandAsync($"{Options.CommandPrefix}lf compare jesus", NetworkTimeout);
                ResponseAssert.Contains(compareResult, "%");
                ResponseAssert.Contains(compareResult, "compatible");
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}lf reset");
            }
        }

        [Fact]
        public async Task LastFmCompare_ComparingSameUsernameToSelf_ReturnsFullMatchWithoutNetworkCall()
        {
            var sameUsername = "same-user-" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunCommandAsync($"{Options.CommandPrefix}lf set {sameUsername}", AckTimeout));
            try
            {
                // The "same username" branch short-circuits before ever constructing a LastFmClient, so this
                // exercises the equality check locally without touching the real API - see WRITING_TESTS.md's
                // "local validation before network call" pattern.
                var result = await RunCommandAsync($"{Options.CommandPrefix}lf compare {sameUsername}", AckTimeout);
                ResponseAssert.Contains(result, "100%");
                ResponseAssert.Contains(result, "compatible");
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}lf reset");
            }
        }

        [Fact]
        public async Task LastFmCompare_WithoutOwnUsernameSet_Fails()
        {
            var ack = await RunCommandAsync($"{Options.CommandPrefix}lf compare {RealLastFmUsername}", AckTimeout);
            Assert.Contains("haven't set", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        private Task<IGatewayUserMessage> RunCommandAsync(string commandText, TimeSpan timeout) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, timeout);
    }
}
