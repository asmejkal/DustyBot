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
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using DustyBot.Helpers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using DustyBot.Entities.TableStorage;
using System.Threading;
using DustyBot.Entities;
using DustyBot.Definitions;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Models;
using SpotifyAPI.Web.Enums;

namespace DustyBot.Modules
{
    [Module("Spotify (beta)", "Show others what you're listening to on Spotify.")]
    class SpotifyModule : Module
    {
        private static readonly Dictionary<string, TimeRangeType> InputStatsPeriodMapping =
            new Dictionary<string, TimeRangeType>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "month", TimeRangeType.ShortTerm },
            { "mo", TimeRangeType.ShortTerm },
            { "6months", TimeRangeType.MediumTerm },
            { "6month", TimeRangeType.MediumTerm },
            { "6mo", TimeRangeType.MediumTerm },
            { "all", TimeRangeType.LongTerm }
        };

        private const string StatsPeriodRegex = "^(?:month|mo|6months|6month|6mo|all)$";

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        private BotConfig Config { get; }
        private CloudTable SpotifyAccountsTable { get; }

        public SpotifyModule(ICommunicator communicator, ISettingsProvider settings, BotConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            Config = config;

            if (config.TableStorageConnectionString != null)
            {
                var storageAccount = CloudStorageAccount.Parse(config.TableStorageConnectionString);
                var storageClient = storageAccount.CreateCloudTableClient();
                SpotifyAccountsTable = storageClient.GetTableReference(TableNames.SpotifyAccountTable);
            }
        }

        [Command("sf", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("spotify", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("sf", "stats", "Shows user's listening stats and habits.", CommandFlags.TypingIndicator)]
        [Alias("spotify", "stats")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `month`, `6months`, `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Stats(ICommand command)
        {
            var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : TimeRangeType.LongTerm;

            var user = command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);
            
            var results = await client.GetUsersTopTracksAsync(period, 50);
            if (!results.Items.Any())
                throw new AbortException("Looks like this user hasn't listened to anything in this time range.");
            
            var features = await client.GetSeveralAudioFeaturesAsync(results.Items.Select(x => x.Id).ToList());
            var albums = results.Items.Select(x => x.Album);
            var artistIds = results.Items.SelectMany(x => x.Artists).Select(x => x.Id).Distinct();
            var artists = await client.GetSeveralArtistsAsync(artistIds.Take(50).ToList());

            var topGenres = artists.Artists.SelectMany(x => x.Genres).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => x.Key);
            var topDecades = albums.Select(x => int.Parse(x.ReleaseDate.Split('-').First()))
                .GroupBy(x => x / 10)
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

            embed.AddField(x => x.WithIsInline(false).WithName("Top genres").WithValue(genresBuilder.ToString()));
            embed.AddField(x => x.WithIsInline(false).WithName("Top decades").WithValue(topDecades.Take(3).WordJoinQuoted("`", " ", " ")));

            void AddPercentageField(string name, double value)
                => embed.AddField(x => x.WithIsInline(false).WithName($"{name} [{FormatPercent(value)}]").WithValue(BuildProgressBar(value)));

            AddPercentageField("Popularity", popularity);
            AddPercentageField("Danceability", danceability);
            AddPercentageField("Acousticness", acousticness);
            AddPercentageField("Energy", energy);
            AddPercentageField("Positivity", positivity);

            embed.AddField(x => x.WithIsInline(false).WithName($"Average tempo").WithValue($"{avgTempo} BPM ({ApproximateClassicalTempo((int)avgTempo)})"));

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("sf", "np", "Shows a user's currently playing song.", CommandFlags.TypingIndicator)]
        [Alias("sf"), Alias("spotify", "np", true), Alias("spotify", true)]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task NowPlaying(ICommand command)
        {
            var user = command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author;
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
                    await command.Reply(Communicator, $"Looks like this user hasn't listened to anything recently.").ConfigureAwait(false);
                    return;
                }

                var item = recents.Items.First();
                track = await client.GetTrackAsync(item.Track.Id);
            }

            var description = new StringBuilder();
            description.AppendLine($"**{FormatLink(track.Name, track.ExternUrls)}** by {BuildArtistsString(track.Artists)}");
            description.AppendLine($"On {FormatLink(track.Album.Name, track.Album.ExternalUrls)}");

            var author = new EmbedAuthorBuilder().WithIconUrl(WebConstants.SpotifyIconUrl);
            if (nowPlaying)
                author.WithName($"{user.Nickname ?? user.Username} is now listening to...");
            else
                author.WithName($"{user.Nickname ?? user.Username} last listened to...");

            var embed = new EmbedBuilder()
                .WithAuthor(author)
                .WithDescription(description.ToString())
                .WithColor(0x31, 0xd9, 0x64);

            if (track.Album.Images.Any())
                embed.WithThumbnailUrl(track.Album.Images.First().Url);

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("sf", "recent", "Shows user's recently played songs.", CommandFlags.TypingIndicator)]
        [Alias("sf", "rc"), Alias("spotify", "recent", true), Alias("spotify", "rc", true)]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Recent(ICommand command)
        {
            var user = command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            const int NumDisplayed = 100;

            var results = await client.GetUsersRecentlyPlayedTracksAsync(50);
            if (!results.Items.Any())
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
        [Alias("sf", "ta"), Alias("sf", "top", "artist", true), Alias("sf", "artists")]
        [Alias("spotify", "top", "artists", true), Alias("spotify", "ta", true), Alias("spotify", "top", "artist", true), Alias("spotify", "artists", true)]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `month`, `6months`, `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Artists(ICommand command)
        {
            var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : TimeRangeType.LongTerm;

            var user = command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            const int NumDisplayed = 100;
            
            var results = await client.GetUsersTopArtistsAsync(period, 50);
            if (!results.Items.Any())
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
        [Alias("sf", "tt"), Alias("sf", "top", "track", true), Alias("sf", "tracks"), Alias("sf", "ts")]
        [Alias("spotify", "top", "tracks", true), Alias("spotify", "tt", true), Alias("spotify", "top", "track", true)]
        [Alias("spotify", "artists", true), Alias("spotify", "ts", true)]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `month`, `6months`, `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "the user (mention or ID); shows your own stats if omitted")]
        public async Task Tracks(ICommand command)
        {
            var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : TimeRangeType.LongTerm;

            var user = command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author;
            var client = await GetClient(user.Id, command.Channel, user.Id != command.Author.Id);

            const int NumDisplayed = 100;

            var results = await client.GetUsersTopTracksAsync(period, 50);
            if (!results.Items.Any())
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
        [Alias("spotify", "reset")]
        public async Task Reset(ICommand command)
        {
            try
            {
                var account = new SpotifyAccount()
                {
                    UserId = command.Author.Id
                };

                var result = await SpotifyAccountsTable.ExecuteAsync(TableOperation.Delete(account.ToTableEntity()));
                await command.ReplySuccess(Communicator, $"Your Spotify account has been disconnected.");
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
            {
                await command.ReplySuccess(Communicator, $"Your Spotify account is not connected.");
            }
        }

        private async Task<SpotifyWebAPI> GetClient(ulong userId, IMessageChannel responseChannel, bool otherUser)
        {
            var account = await GetUserAccount(userId);
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

        private async Task<SpotifyAccount> GetUserAccount(ulong id)
        {
            var filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, id.ToString());
            var results = await SpotifyAccountsTable.ExecuteQueryAsync(new TableQuery<SpotifyAccountTableEntity>().Where(filter), CancellationToken.None);
            if (!results.Any())
                return null;

            return results.Single().ToModel();
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
    }
}
