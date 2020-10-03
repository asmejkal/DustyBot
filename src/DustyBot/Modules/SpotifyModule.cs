using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using DustyBot.Helpers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using DustyBot.Definitions;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Models;
using SpotifyAPI.Web.Enums;
using System.Text.RegularExpressions;
using DustyBot.Database.Services;
using DustyBot.Core.Formatting;
using System.Threading;

namespace DustyBot.Modules
{
    [Module("Spotify", "Show others what you're listening to on Spotify.")]
    class SpotifyModule : Module
    {
        private static readonly IReadOnlyDictionary<string, TimeRangeType> InputStatsPeriodMapping =
            new Dictionary<string, TimeRangeType>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "month", TimeRangeType.ShortTerm },
            { "mo", TimeRangeType.ShortTerm },
            { "6months", TimeRangeType.MediumTerm },
            { "6month", TimeRangeType.MediumTerm },
            { "6mo", TimeRangeType.MediumTerm },
            { "all", TimeRangeType.LongTerm }
        };

        private static readonly Regex SpotifyTrackIdRegex = new Regex(@"(?:https:\/\/open.spotify.com\/track\/([^?#\s]+))|(?:spotify:track:([^\s]+))");

        private static readonly IList<string> PitchClasses = new[] { "C", "C#" , "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private const string StatsPeriodRegex = "^(?:month|mo|6months|6month|6mo|all)$";

        private ICommunicator Communicator { get; }
        private ISettingsService Settings { get; }
        private ISpotifyAccountsService AccountsService { get; }
        private BotConfig Config { get; }

        public SpotifyModule(ICommunicator communicator, ISettingsService settings, ISpotifyAccountsService accountsService, BotConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            AccountsService = accountsService;
            Config = config;
        }

        [Command("sf", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("spotify", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("sf", "np", "Shows a user's currently playing song.", CommandFlags.TypingIndicator)]
        [Alias("sf")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        [Comment("Use `sf np detail` to see an analysis of this track.")]
        public Task NowPlaying(ICommand command)
        {
            return NowPlayingInner(command, false);
        }

        [Command("sf", "np", "detail", "Shows a user's currently playing song with an analysis.", CommandFlags.TypingIndicator)]
        [Alias("sf", "detail"), Alias("sf", "np", "details", true), Alias("sf", "details", true)]
        [Alias("sf", "np", "stats", true)]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public Task NowPlayingAnalyse(ICommand command)
        {
            return NowPlayingInner(command, true);
        }

        public async Task NowPlayingInner(ICommand command, bool analyse)
        {
            var user = command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            var result = await client.GetPlayingTrackAsync();
            bool nowPlaying = result != null && result.IsPlaying;

            var track = result.Item;
            if (track == null)
            {
                // Pull from recents
                var recents = await client.GetUsersRecentlyPlayedTracksAsync(1);
                if (!recents.Items.Any())
                {
                    await command.Reply(Communicator, $"Looks like this user hasn't listened to anything recently.");
                    return;
                }

                var item = recents.Items.First();
                track = await client.GetTrackAsync(item.Track.Id);
            }

            var title = (user.Nickname ?? user.Username) + (nowPlaying ? " is now listening to..." : " last listened to...");
            var embed = await PrepareTrackEmbed(client, track, title, analyse);

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("sf", "track", "Shows track info and analysis.", CommandFlags.TypingIndicator)]
        [Parameter("Track", ParameterType.String, ParameterFlags.Remainder, "name or Spotify URI/Link of the track")]
        [Example("Laboum Between Us")]
        [Example("spotify:track:5crqbWLP7Jb0s86hnm0XDA")]
        [Example("https://open.spotify.com/track/5crqbWLP7Jb0s86hnm0XDA")]
        public async Task Track(ICommand command)
        {
            var client = await GetClient();
            var trackIdMatch = SpotifyTrackIdRegex.Match(command["Track"]);
            FullTrack track;
            if (!trackIdMatch.Success)
            {
                var sclient = await SpotifyClient.Create(Config.SpotifyId, Config.SpotifyKey);

                var trackId = await sclient.SearchTrackId(command["Track"]);
                //var searchResult = await client.SearchItemsAsync($"q={command["Track"]}", SearchType.Track, 1);
                //if (!searchResult.Tracks.Items.Any())
                if (trackId == null)
                    throw new AbortException($"Search for track `{command["Track"]}` returned no results. Consider passing a Spotify link instead.");

                //track = searchResult.Tracks.Items.First();
                track = await client.GetTrackAsync(trackId);
            }
            else
            {
                var trackId = trackIdMatch.Groups.Values.Skip(1).First(x => !string.IsNullOrEmpty(x.Value)).Value;
                track = await client.GetTrackAsync(trackId);
                if (track.HasError())
                    throw new AbortException($"Search for track with ID `{trackId}` returned no results. Are you sure the ID is correct?");
            }

            var embed = await PrepareTrackEmbed(client, track, "Track analysis", true);

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("sf", "stats", "Shows user's listening stats and habits.", CommandFlags.TypingIndicator)]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `month`, `6months`, `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Stats(ICommand command)
        {
            var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : TimeRangeType.LongTerm;

            var user = command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            var results = await client.GetUsersTopTracksAsync(period, 50);
            if (!results?.Items.Any() ?? true)
                throw new AbortException("Looks like this user hasn't listened to anything in this time range.");

            var features = await client.GetSeveralAudioFeaturesAsync(results.Items.Select(x => x.Id).ToList());
            var albums = results.Items.Select(x => x.Album);
            var artistIds = results.Items.SelectMany(x => x.Artists).Select(x => x.Id).Distinct();
            var artists = await client.GetSeveralArtistsAsync(artistIds.Take(50).ToList());

            var topGenres = artists.Artists.SelectMany(x => x.Genres).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => x.Key);
            var topDecades = albums.Select(x => int.Parse(x.ReleaseDate.Split('-').First()))
                .GroupBy(x => x / 10)
                .Where(x => x.Count() >= 5)
                .OrderByDescending(x => x.Count())
                .Select(x => $"{x.Key * 10}'s");

            var danceability = features.AudioFeatures.Average(x => x.Danceability);
            var acousticness = features.AudioFeatures.Average(x => x.Acousticness);
            var instrumentalness = features.AudioFeatures.Average(x => x.Instrumentalness);
            var energy = features.AudioFeatures.Average(x => x.Energy);
            var positivity = features.AudioFeatures.Average(x => x.Valence);
            var popularity = results.Items.Average(x => x.Popularity) / 100;
            var avgTempo = Math.Round(features.AudioFeatures.Average(x => x.Tempo));

            var author = new EmbedAuthorBuilder()
                .WithIconUrl(WebConstants.SpotifyIconUrl)
                .WithName($"{user.Nickname ?? user.Username}'s listening habits {FormatStatsPeriod(period)}");

            var embed = new EmbedBuilder()
                .WithAuthor(author)
                .WithColor(0x31, 0xd9, 0x64)
                .WithFooter($"Based on your top 50 tracks");

            var genresBuilder = new StringBuilder();
            int lineLength = 0;
            foreach (var genre in topGenres.Take(4))
            {
                var newLine = genre.Length + lineLength > 24;
                genresBuilder.Append((newLine ? "\n" : "") + $"`{genre}` ");
                lineLength = newLine ? genre.Length : (lineLength + genre.Length);
            }

            embed.AddField(x => x.WithIsInline(true).WithName("Top genres").WithValue(genresBuilder.Length > 0 ? genresBuilder.ToString() : "`unknown`"));
            embed.AddField(x => x.WithIsInline(true).WithName("Top decades").WithValue(topDecades.Take(3).WordJoinQuoted("`", " ", " ")));
            embed.AddField(x => x.WithIsInline(true).WithName($"Average tempo").WithValue($"{avgTempo} BPM ({ApproximateClassicalTempo((int)avgTempo)})"));

            AddPercentageField(embed, "Popularity", popularity);
            AddPercentageField(embed, "Instrumental", instrumentalness);
            AddPercentageField(embed, "Acoustic", acousticness);
            AddPercentageField(embed, "Danceability", danceability);
            AddPercentageField(embed, "Energy", energy);
            AddPercentageField(embed, "Positivity", positivity);

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("sf", "recent", "Shows user's recently played songs.", CommandFlags.TypingIndicator)]
        [Alias("sf", "rc")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Recent(ICommand command)
        {
            var user = command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            const int NumDisplayed = 100;

            var results = await client.GetUsersRecentlyPlayedTracksAsync(50);
            if (!results?.Items.Any() ?? true)
                throw new AbortException("Looks like this user hasn't listened to anything recently.");

            var pages = new PageCollectionBuilder();
            var place = 1;
            var now = DateTime.UtcNow;
            foreach (var item in results.Items.Take(NumDisplayed))
            {
                var track = item.Track;
                var when = now - item.PlayedAt;
                
                pages.AppendLine($"`{place++}>` **{FormatLink(track.Name, track.ExternUrls)}** by {BuildArtistsString(track.Artists)}_ – {when.SimpleFormat()} ago_");
            }

            var embedFactory = new Func<EmbedBuilder>(() =>
            {
                var author = new EmbedAuthorBuilder()
                    .WithIconUrl(WebConstants.SpotifyIconUrl)
                    .WithName($"{user.Nickname ?? user.Username} last listened to...");

                var embed = new EmbedBuilder()
                    .WithAuthor(author)
                    .WithColor(0x31, 0xd9, 0x64);

                return embed;
            });

            await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
        }

        [Command("sf", "top", "artists", "Shows user's top artists.", CommandFlags.TypingIndicator)]
        [Alias("sf", "ta"), Alias("sf", "top", "artist", true), Alias("sf", "topartists", true), Alias("sf", "topartist", true), Alias("sf", "artists")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `month`, `6months`, `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Artists(ICommand command)
        {
            var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : TimeRangeType.LongTerm;

            var user = command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            const int NumDisplayed = 100;
            
            var results = await client.GetUsersTopArtistsAsync(period, 50);
            if (!results?.Items.Any() ?? true)
                throw new AbortException("Looks like this user hasn't listened to anything in this time range.");

            var pages = new PageCollectionBuilder();
            var place = 1;
            var now = DateTime.UtcNow;
            foreach (var item in results.Items.Take(NumDisplayed))
                pages.AppendLine($"`#{place++}` **{FormatLink(item.Name, item.ExternalUrls, false)}**");

            var embedFactory = new Func<EmbedBuilder>(() =>
            {
                var author = new EmbedAuthorBuilder()
                    .WithIconUrl(WebConstants.SpotifyIconUrl)
                    .WithName($"{user.Nickname ?? user.Username}'s top artists {FormatStatsPeriod(period)}");

                var embed = new EmbedBuilder()
                    .WithAuthor(author)
                    .WithColor(0x31, 0xd9, 0x64)
                    .WithFooter($"Recent listens hold more weight")
                    .WithThumbnailUrl(results.Items.First().Images.FirstOrDefault()?.Url);

                return embed;
            });

            await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
        }

        [Command("sf", "top", "tracks", "Shows user's top tracks.", CommandFlags.TypingIndicator)]
        [Alias("sf", "tt"), Alias("sf", "top", "track", true), Alias("sf", "toptracks", true), Alias("sf", "toptrack", true), Alias("sf", "tracks"), Alias("sf", "ts")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `month`, `6months`, `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Tracks(ICommand command)
        {
            var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : TimeRangeType.LongTerm;

            var user = command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            const int NumDisplayed = 100;

            var results = await client.GetUsersTopTracksAsync(period, 50);
            if (!results?.Items.Any() ?? true)
                throw new AbortException("Looks like this user hasn't listened to anything in this time range.");

            var pages = new PageCollectionBuilder();
            var place = 1;
            var now = DateTime.UtcNow;
            foreach (var item in results.Items.Take(NumDisplayed))
                pages.AppendLine($"`#{place++}` **{FormatLink(item.Name, item.ExternUrls)}** by {BuildArtistsString(item.Artists)}");

            var embedFactory = new Func<EmbedBuilder>(() =>
            {
                var author = new EmbedAuthorBuilder()
                    .WithIconUrl(WebConstants.SpotifyIconUrl)
                    .WithName($"{user.Nickname ?? user.Username}'s top tracks {FormatStatsPeriod(period)}");

                var embed = new EmbedBuilder()
                    .WithAuthor(author)
                    .WithColor(0x31, 0xd9, 0x64)
                    .WithFooter($"Recent listens hold more weight");

                return embed;
            });

            await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
        }

        [Command("sf", "reset", "Disconnect your Spotify account.", CommandFlags.DirectMessageAllow)]
        [Alias("sf", "disconnect")]
        public async Task Reset(ICommand command)
        {
            try
            {
                await AccountsService.RemoveUserAccountAsync(command.Author.Id, CancellationToken.None);
                await command.ReplySuccess(Communicator, $"Your Spotify account has been disconnected.");
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
            {
                await command.ReplySuccess(Communicator, $"Your Spotify account is not connected.");
            }
        }

        private async Task<SpotifyWebAPI> GetClient(ulong userId, IMessageChannel responseChannel, bool otherUser)
        {
            var account = await AccountsService.GetUserAccountAsync(userId, CancellationToken.None);
            if (account != null)
            {
                try
                {
                    var result = await SpotifyHelpers.RefreshToken(account.RefreshToken, Config.SpotifyId, Config.SpotifyKey);
                    return new SpotifyWebAPI()
                    {
                        AccessToken = result.Token,
                        TokenType = "Bearer"
                    };
                }
                catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.BadRequest)
                {

                }
            }

            var author = new EmbedAuthorBuilder()
                    .WithName(otherUser ? "Account not connected" : "Connect your Spotify")
                    .WithUrl(WebConstants.SpotifyConnectUrl)
                    .WithIconUrl(WebConstants.SpotifyIconUrl);

            var embed = new EmbedBuilder()
                .WithAuthor(author)
                .WithDescription((otherUser ? "This user hasn't connected their account yet." : "You have not connected your account yet.") + $" Click [here]({WebConstants.SpotifyConnectUrl}) to connect.");

            await responseChannel.SendMessageAsync(embed: embed.Build());
            throw new AbortException();
        }

        private async Task<SpotifyWebAPI> GetClient()
        {
            var token = await SpotifyHelpers.GetClientToken(Config.SpotifyId, Config.SpotifyKey);
            return new SpotifyWebAPI()
            {
                AccessToken = token.Token,
                TokenType = "Bearer"
            };
        }

        private string FormatLink(string text, Dictionary<string, string> externUrls, bool trim = true)
        {
            text = text.Truncate(trim ? 22 : int.MaxValue);
            if (externUrls.ContainsKey("spotify"))
                return DiscordHelpers.BuildMarkdownUri(text, externUrls["spotify"]);
            else
                return text;
        }

        private string BuildArtistsString(IEnumerable<SimpleArtist> artists)
        {
            if (!artists.Any())
                return "**Unknown**";

            return "**" + string.Join("**, **", artists.Select(x => FormatLink(x.Name, x.ExternalUrls))) + "**";
        }

        TimeRangeType ParseStatsPeriod(string input)
            => InputStatsPeriodMapping.TryGetValue(input, out var result) ? result : throw new IncorrectParametersCommandException("Invalid time period.");

        string FormatStatsPeriod(TimeRangeType period)
        {
            switch (period)
            {
                case TimeRangeType.ShortTerm: return "in the last month";
                case TimeRangeType.MediumTerm: return "in the last 6 months";
                case TimeRangeType.LongTerm: return "";
                default: throw new ArgumentException($"Unknown value {period}");
            }
        }

        string FormatPercent(double value) => $"{Math.Round(value * 100)}%";

        string BuildProgressBar(double value)
        {
            var builder = new StringBuilder();
            double i;
            // Added invisible \ufeff characters to trick mobile into using small emoji versions...
            for (i = 0; i < value; i += 0.2)
                builder.Append(":green_square: \ufeff");

            for (; i < 1; i += 0.2)
                builder.Append(":white_large_square: \ufeff");

            return builder.ToString();
        }

        string ApproximateClassicalTempo(int bpm)
        {
            if (bpm < 40)
                return "Grave";
            else if (bpm < 60)
                return "Largo";
            else if (bpm < 66)
                return "Larghetto";
            else if (bpm < 76)
                return "Adagio";
            else if (bpm < 108)
                return "Andante";
            else if (bpm < 120)
                return "Moderato";
            else if (bpm < 168)
                return "Allegro";
            else if (bpm < 200)
                return "Presto";
            else
                return "Prestissimo";
        }

        string PrintKey(int pitchClass, int mode) => $"{PitchClasses[pitchClass]} " + (mode == 1 ? "major" : "minor");

        void AddPercentageField(EmbedBuilder embed, string name, double value)
                => embed.AddField(x => x.WithIsInline(true).WithName($"{name} [{FormatPercent(value)}]").WithValue(BuildProgressBar(value)));

        void AddBarField(EmbedBuilder embed, string title, double value)
                => embed.AddField(x => x.WithIsInline(true).WithName($"{title}").WithValue(BuildProgressBar(value)));

        private async Task<EmbedBuilder> PrepareTrackEmbed(SpotifyWebAPI client, FullTrack track, string title, bool analyse)
        {
            var description = new StringBuilder();
            description.AppendLine($"**{FormatLink(track.Name, track.ExternUrls, false)}** by {BuildArtistsString(track.Artists)}");
            description.AppendLine($"On {FormatLink(track.Album.Name, track.Album.ExternalUrls, false)}");

            var author = new EmbedAuthorBuilder()
                .WithIconUrl(WebConstants.SpotifyIconUrl)
                .WithName(title);

            var embed = new EmbedBuilder()
                .WithAuthor(author)
                .WithColor(0x31, 0xd9, 0x64)
                .WithDescription(description.ToString());

            if (track.Album.Images.Any())
                embed.WithThumbnailUrl(track.Album.Images.First().Url);

            if (analyse)
            {
                embed.WithFooter($"Calculated by Spotify");

                var analysis = await client.GetAudioFeaturesAsync(track.Id);
                if (analysis.HasError())
                    throw new AbortException("Couldn't find an analysis for this track.");

                var album = await client.GetAlbumAsync(track.Album.Id);
                if (album.HasError())
                    throw new AbortException("Failed to find this track's album.");

                var genres = (IEnumerable<string>)album.Genres;
                if (!genres.Any())
                {
                    var artists = await client.GetSeveralArtistsAsync(track.Artists.Select(x => x.Id).ToList());
                    genres = artists.Artists.SelectMany(x => x.Genres).Distinct();
                }

                var genresBuilder = new StringBuilder();
                int lineLength = 0;
                foreach (var genre in genres.Take(8))
                {
                    var newLine = genre.Length + lineLength > 24;
                    genresBuilder.Append((newLine ? "\n" : "") + $"`{genre}` ");
                    lineLength = newLine ? genre.Length : (lineLength + genre.Length);
                }

                var genresString = genresBuilder.Length > 0 ? genresBuilder.ToString() : "`unknown`";
                var tempo = (int)Math.Round(analysis.Tempo);
                var duration = TimeSpan.FromMilliseconds(track.DurationMs);

                embed.AddField(x => x.WithIsInline(true).WithName("Genres").WithValue(genresString));
                embed.AddField(x => x.WithIsInline(true).WithName("Label").WithValue(album.Label ?? "Unknown"));
                embed.AddField(x => x.WithIsInline(true).WithName("Release date").WithValue(album.ReleaseDate));
                embed.AddField(x => x.WithIsInline(true).WithName("Duration").WithValue($"{duration.ToString(@"mm\:ss", GlobalDefinitions.Culture)}"));
                embed.AddField(x => x.WithIsInline(true).WithName("Tempo").WithValue($"{tempo} BPM ({ApproximateClassicalTempo(tempo)})"));
                embed.AddField(x => x.WithIsInline(true).WithName("Key (estimated)").WithValue(PrintKey(analysis.Key, analysis.Mode)));
                
                AddPercentageField(embed, "Popularity", track.Popularity / 100d);
                AddBarField(embed, $"Instrumental [{(analysis.Instrumentalness > 0.5 ? "Yes" : "No")}]", analysis.Instrumentalness);
                AddBarField(embed, $"Acoustic [{(analysis.Acousticness > 0.5 ? "Yes" : "No")}]", analysis.Acousticness);
                AddPercentageField(embed, "Danceability", analysis.Danceability);
                AddPercentageField(embed, "Energy", analysis.Energy);
                AddPercentageField(embed, "Positivity", analysis.Valence);
            }           

            return embed;
        }
    }
}
