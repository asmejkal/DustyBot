using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for SpotifyModule ("sf"/"spotify" group, old stack - not yet migrated). Acks are
    /// correlated by timestamp, not reply, since the old framework never reply-links.
    ///
    /// Unlike LastFmModule's public-by-username profiles, every per-user command ("np", "stats", "recent", "top
    /// artists/tracks") needs a real Spotify account linked via a browser OAuth flow - neither tester bot can
    /// complete that, so a connected account can only ever belong to an actual human.
    /// EnsureRealAccountUserIdAsync prompts a person to connect one and reply in the run channel; that reply's
    /// author becomes the target for every real-account test in this class (once per suite run, via
    /// RunOnceGate). "sf track" needs no account at all (client-credentials flow), so its tests run without any
    /// human interaction.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class SpotifyModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(30);
        private static readonly RunOnceGate ConnectGate = new();
        private static Snowflake? _realAccountUserId;

        private readonly ITestOutputHelper _output;

        public SpotifyModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task NowPlaying_InvokedWithoutConnectedAccount_ShowsConnectPrompt()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf np", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("connect your spotify", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("you have not connected", embed.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NowPlaying_ForOtherUnconnectedUser_ShowsAccountNotConnected()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf np <@{lowPrivId}>", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("account not connected", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hasn't connected", embed.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NowPlayingDetail_InvokedWithoutConnectedAccount_ShowsConnectPrompt()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf np detail", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("connect your spotify", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Stats_InvokedWithoutConnectedAccount_ShowsConnectPrompt()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf stats", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("connect your spotify", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Recent_InvokedWithoutConnectedAccount_ShowsConnectPrompt()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf recent", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("connect your spotify", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TopArtists_InvokedWithoutConnectedAccount_ShowsConnectPrompt()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf top artists", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("connect your spotify", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TopTracks_InvokedWithoutConnectedAccount_ShowsConnectPrompt()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf top tracks", AckTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("connect your spotify", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Reset_WithoutConnectedAccount_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}sf reset", AckTimeout);

            ResponseAssert.IsSuccess(ack);
            Assert.Contains("not connected", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// "Period" is a ParameterType.Regex, Optional parameter - an Optional parameter that fails its format
        /// check is skipped, not rejected, so it falls through to the equally Optional "User" parameter, fails
        /// that too, and ends up unconsumed as "too many parameters" instead of ever reaching
        /// ParseStatsPeriod's own "Invalid time period." exception.
        /// </summary>
        [Fact]
        public async Task Stats_UnrecognizedPeriodToken_FailsWithTooManyParameters()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}sf stats notaperiod", AckTimeout);

            ResponseAssert.IsFailure(ack);
            Assert.Contains("too many parameters", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Track_ByName_Succeeds()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf track Rick Astley Never Gonna Give You Up", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Equal("Track analysis", embed.Author?.Name);
            Assert.Contains(embed.Fields, f => f.Name.Contains("Tempo", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(embed.Fields, f => f.Name.Contains("Genres", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Track_BySpotifyLink_Succeeds()
        {
            var result = await RunAdminCommandAsync(
                $"{Options.CommandPrefix}sf track https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Equal("Track analysis", embed.Author?.Name);
            Assert.Contains("Never Gonna Give You Up", embed.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Track_ByUnrecognizableQuery_Fails()
        {
            var result = await RunAdminCommandAsync(
                $"{Options.CommandPrefix}sf track {Guid.NewGuid():N} {Guid.NewGuid():N} nonexistent gibberish query", NetworkTimeout);

            Assert.Contains("returned no results", result.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Track_ByUnknownSpotifyId_Fails()
        {
            var result = await RunAdminCommandAsync(
                $"{Options.CommandPrefix}sf track https://open.spotify.com/track/0000000000000000000000", NetworkTimeout);

            Assert.Contains("returned no results", result.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RealAccount_NowPlaying_ShowsTrackInfo()
        {
            var userId = await EnsureRealAccountUserIdAsync();
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf np <@{userId}>", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("listen", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
        }

        [Fact]
        public async Task RealAccount_NowPlayingDetail_ShowsAnalysisFields()
        {
            var userId = await EnsureRealAccountUserIdAsync();
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf np detail <@{userId}>", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains(embed.Fields, f => f.Name.Contains("Tempo", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(embed.Fields, f => f.Name.Contains("Key", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task RealAccount_Stats_ShowsListeningHabits()
        {
            var userId = await EnsureRealAccountUserIdAsync();
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf stats <@{userId}>", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("listening habits", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(embed.Fields, f => f.Name.Contains("Top genres", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(embed.Fields, f => f.Name.Contains("Average tempo", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task RealAccount_Recent_ShowsRecentTracks()
        {
            var userId = await EnsureRealAccountUserIdAsync();
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf recent <@{userId}>", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("listened to", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
        }

        [Fact]
        public async Task RealAccount_TopArtists_ShowsTopArtists()
        {
            var userId = await EnsureRealAccountUserIdAsync();
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf top artists <@{userId}>", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("top artists", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
        }

        [Fact]
        public async Task RealAccount_TopTracks_ShowsTopTracks()
        {
            var userId = await EnsureRealAccountUserIdAsync();
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}sf top tracks <@{userId}>", NetworkTimeout);

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("top tracks", embed.Author?.Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
        }

        /// <summary>
        /// Extracts the connect URL from a live "sf np" prompt (nothing here hardcodes it), posts it with
        /// instructions, and waits for a human reply - that reply's author becomes the real-account target for
        /// the rest of the run. Gated by RunOnceGate; a timeout leaves the gate unlatched so the next
        /// real-account test retries the prompt.
        /// </summary>
        private async Task<Snowflake> EnsureRealAccountUserIdAsync()
        {
            await ConnectGate.RunOnceAsync(async () =>
            {
                var promptResult = await RunAdminCommandAsync($"{Options.CommandPrefix}sf np", AckTimeout);
                var connectEmbed = Assert.Single(promptResult.Embeds);
                var connectUrl = connectEmbed.Author?.Url
                    ?? throw new InvalidOperationException("Expected the connect-prompt embed to have an author URL.");

                var before = DateTimeOffset.UtcNow;
                await Fixture.Admin.SendMessageAsync(
                    Fixture.ChannelId,
                    "**SpotifyModuleTests needs a real, connected Spotify account to test \"sf np/stats/recent/top\" " +
                    $"for real.** Please open {connectUrl}, log in with a Discord account that's a member of this " +
                    "test guild (any account - it just needs to actually be a guild member so `sf np <@you>` can " +
                    "resolve it), and connect a Spotify account that has some recent/top listening history. Once " +
                    $"connected, reply in this channel (any message) within {Options.SpotifyConnectTimeout.TotalMinutes:0} minutes.");

                var reply = await Fixture.Admin.WaitForMessageAsync(
                    m => m.ChannelId == Fixture.ChannelId && !m.Author.IsBot && m.Id.CreatedAt >= before,
                    Options.SpotifyConnectTimeout);

                _realAccountUserId = reply.Author.Id;
            });

            return _realAccountUserId
                ?? throw new InvalidOperationException("No real Spotify-connected account was captured - the connect prompt likely timed out.");
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText, TimeSpan timeout) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, timeout);
    }
}
