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
using System.IO;
using Newtonsoft.Json.Linq;

namespace DustyBot.Modules
{
    [Module("Last.fm", "Show others what you're listening to.")]
    class LastFmModule : Module
    {
        class LfMatch<T>
            where T : ILfEntity
        {
            public object Id => First.Id;
            public double Score { get; }

            public T First { get; }
            public T Second { get; }

            internal LfMatch(T first, T second, double score)
            {
                First = first;
                Second = second;
                Score = score;
            }
        }

        private static readonly Dictionary<string, LfStatsPeriod> InputStatsPeriodMapping =
            new Dictionary<string, LfStatsPeriod>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "day", LfStatsPeriod.Day },
            { "week", LfStatsPeriod.Week },
            { "month", LfStatsPeriod.Month },
            { "mo", LfStatsPeriod.Month },
            { "3months", LfStatsPeriod.QuarterYear },
            { "3month", LfStatsPeriod.QuarterYear },
            { "3mo", LfStatsPeriod.QuarterYear },
            { "6months", LfStatsPeriod.HalfYear },
            { "6month", LfStatsPeriod.HalfYear },
            { "6mo", LfStatsPeriod.HalfYear },
            { "year", LfStatsPeriod.Year },
            { "all", LfStatsPeriod.Overall }
        };

        private const string StatsPeriodRegex = "^(?:day|week|w|month|mo|3months|3month|3mo|6months|6month|6mo|year|y|all)$";

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public LastFmModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }

        [Command("lf", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("lf"), Alias("lastfm"), Alias("lastfm", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("lf", "np", "Shows what song you or someone else is currently playing on Last.fm.", CommandFlags.TypingIndicator)]
        [Alias("np")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional | ParameterFlags.Remainder, "the user (mention, ID or Last.fm username); uses your Last.fm if omitted")]
        [Comment("Also shows the song's position among user's top 100 listened tracks.")]
        public async Task NowPlaying(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);

                var client = new LastFmClient(settings.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey);
                var topTracksTask = client.GetTopTracks(LfStatsPeriod.Month, 100);
                var result = await client.GetRecentTracks(count: 1);

                var tracks = result.tracks.ToList();
                var nowPlaying = result.nowPlaying;
                if (!tracks.Any())
                {
                    await command.Reply(Communicator, $"Looks like this user doesn't have any scrobbles yet...").ConfigureAwait(false);
                    return;
                }

                var current = await client.GetTrackInfo((string)tracks[0].artist?.name, (string)tracks[0].name);
                
                // Description
                var description = new StringBuilder();
                description.AppendLine($"**{FormatDynamicLink(current ?? tracks[0])}** by **{FormatDynamicLink(current?.artist ?? tracks[0].artist)}**");
                if (!string.IsNullOrEmpty((string)current?.album?.title))
                    description.AppendLine($"On {DiscordHelpers.BuildMarkdownUri((string)current.album.title, (string)current.album.url)}");
                else if (!string.IsNullOrEmpty((string)tracks[0].album?["#text"]))
                    description.AppendLine($"On {tracks[0].album?["#text"]}");

                var embed = new EmbedBuilder()
                    .WithDescription(description.ToString())
                    .WithColor(0xd9, 0x23, 0x23);

                // Image
                var image = LastFmClient.GetLargestImage(current?.album?.image);
                if (string.IsNullOrEmpty(image))
                    image = LastFmClient.GetLargestImage(tracks[0].image);

                if (!string.IsNullOrEmpty(image))
                    embed.WithThumbnailUrl(image);

                // Title
                var author = new EmbedAuthorBuilder().WithIconUrl(HelpBuilder.LfIconUrl);
                if (!settings.Anonymous)
                    author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                if (nowPlaying)
                    author.WithName($"{user} is now listening to...");
                else
                    author.WithName($"{user} last listened to...");

                embed.WithAuthor(author);

                // Playcount
                var playCount = (int)(current?.userplaycount ?? 0) + (nowPlaying ? 1 : 0);
                if (playCount == 1 && current?.userplaycount != null)
                    embed.WithFooter($"First listen");
                else if (playCount > 1)
                    embed.WithFooter($"{playCount.ToEnglishOrdinal()} listen");

                // Month placement
                {
                    var topTracks = await topTracksTask;

                    int counter = 0, placement = 0, placementPlaycount = int.MaxValue;
                    foreach (var track in topTracks)
                    {
                        counter++;
                        if (placementPlaycount > track.Playcount)
                        {
                            placementPlaycount = track.Playcount;
                            placement = counter;
                        }

                        if (string.Compare(track.Url, (string)(current?.url ?? tracks[0].url), true) == 0)
                        {
                            var footer = placement == 1 ? "Most played track this month" : $"{placement.ToEnglishOrdinal()} most played this month";
                            if (embed.Footer != null)
                                embed.Footer.Text += " • " + footer;
                            else
                                embed.WithFooter(footer);

                            break;
                        }
                    }
                }

                // Previous
                if (nowPlaying && tracks.Count > 1)
                {
                    var previous = tracks[1];
                    embed.AddField(x => x.WithName("Previous").WithValue($"{FormatDynamicLink(previous)} by {FormatDynamicLink(previous.artist)}"));
                }

                await command.Message.Channel.SendMessageAsync(embed: embed.Build());
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach Last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "np", "spotify", "Searches and posts a Spotify link to what you're currently listening.", CommandFlags.TypingIndicator)]
        [Alias("np", "spotify")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional | ParameterFlags.Remainder, "the user (mention, ID or Last.fm username); uses your Last.fm if omitted")]
        public async Task NowPlayingSpotify(ICommand command)
        {
            var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
            var config = await Settings.ReadGlobal<BotConfig>();

            var lfm = new LastFmClient(settings.LastFmUsername, config.LastFmKey);
            var nowPlayingTask = lfm.GetRecentTracks(count: 1);

            var spotify = await SpotifyClient.Create(config.SpotifyId, config.SpotifyKey);

            var nowPlaying = (await nowPlayingTask).tracks.FirstOrDefault();
            if (nowPlaying == null)
            {
                await command.Reply(Communicator, $"Looks like this user doesn't have any scrobbles yet...").ConfigureAwait(false);
                return;
            }

            var url = await spotify.SearchTrackUrl($"{nowPlaying.name} artist:{nowPlaying.artist.name}");
            if (string.IsNullOrEmpty(url))
            {
                await command.Reply(Communicator, $"Can't find this track on Spotify...").ConfigureAwait(false);
                return;
            }

            await command.Reply(Communicator, $"<:sf:621852106235707392> **{user} is now listening to...**\n" + url).ConfigureAwait(false);
        }

        [Command("lf", "compare", "Checks how compatible your music taste is with someone else.", CommandFlags.TypingIndicator)]
        [Alias("lf", "cmp"), Alias("lf", "compatibility")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year` (default) and `all`")]
        [Parameter("User", ParameterType.GuildUserOrName, "the other user (mention, ID or Last.fm username)")]
        [Comment("If you choose a short time period, there might not be enough data to get meaningful results.")]
        public async Task LastFmCompare(ICommand command)
        {
            try
            {
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LfStatsPeriod.Year;

                // Get usernames
                var settings = new
                {
                    first = (await GetLastFmSettings((IGuildUser)command.Author)).settings,
                    second = (await GetLastFmSettings(command["User"])).settings,
                };

                if (settings.first.LastFmUsername == settings.second.LastFmUsername)
                {
                    var sameEmbed = new EmbedBuilder()
                        .WithAuthor(x => x.WithName($"Your music taste is {FormatPercent(1)} compatible!")
                        .WithIconUrl(HelpBuilder.LfIconUrl))
                        .WithDescription("What did you expect?")
                        .WithFooter($"Based on {FormatStatsDataPeriod(period)}");

                    await command.Channel.SendMessageAsync(embed: sameEmbed.Build());
                    return;
                }

                // Prepare data
                var clients = new
                {
                    first = new LastFmClient(settings.first.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey),
                    second = new LastFmClient(settings.second.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey)
                };

                var playcounts = new
                {
                    first = clients.first.GetTotalPlaycount(period),
                    second = clients.second.GetTotalPlaycount(period)
                };

                var artists = new
                {
                    first = clients.first.GetArtistScores(period, 999, playcounts.first),
                    second = clients.second.GetArtistScores(period, 999, playcounts.second)
                };

                var albums = new
                {
                    first = clients.first.GetAlbumScores(period, 999, playcounts.first),
                    second = clients.second.GetAlbumScores(period, 999, playcounts.second)
                };

                var tracks = new
                {
                    first = clients.first.GetTrackScores(period, 999, playcounts.first),
                    second = clients.second.GetTrackScores(period, 999, playcounts.second)
                };

                var artistMatches = GetMatches((await artists.first), (await artists.second)).ToList();
                var artistScore = artistMatches.Aggregate(0.0, (x, y) => x + y.Score);

                var albumMatches = GetMatches((await albums.first), (await albums.second)).ToList();
                var albumScore = albumMatches.Aggregate(0.0, (x, y) => x + y.Score);

                var trackMatches = GetMatches((await tracks.first), (await tracks.second)).ToList();
                var trackScore = trackMatches.Aggregate(0.0, (x, y) => x + y.Score);

                // Listening to the same artist, but different albums -> 50% score
                // Listening to the same album, but different tracks -> 75% score
                // Listening to the same tracks -> 100% score
                var compatibility = Math.Pow(artistScore * 0.5 + albumScore * 0.25 + trackScore * 0.25, 0.4);

                var embed = new EmbedBuilder()
                    .WithAuthor(x => x.WithName($"Your music taste is {FormatPercent(compatibility)} compatible!")
                    .WithIconUrl(HelpBuilder.LfIconUrl))
                    .WithDescription("Based on how many times you've listened to the same music.")
                    .WithFooter($"Based on {FormatStatsDataPeriod(period)}")
                    .WithColor(0xd9, 0x23, 0x23);

                const string noData = "Nothing to show here";
                string ListFormat<T>(List<LfMatch<T>> data, Func<T, bool, string> formatter)
                    where T : ILfEntity
                {
                    if (data.Any())
                        return "Top matches are " 
                            + data.Take(3).Select(y => $"{formatter(y.First, true)} ({FormatPercent(y.Score)})").WordJoin() 
                            + ".";
                    else
                        return noData;
                }

                embed.AddField(x => x
                    .WithName($"You listen to the same artists {FormatPercent(artistScore)} of the time.")
                    .WithValue(ListFormat(artistMatches, FormatArtistLink)));

                // Only show more stats if we have sufficient data
                if (artistScore != 0 && artistMatches.Count > 1 && GetCommonPlays(artistMatches) > 20)
                {
                    var albumMatchRatio = (1 / artistScore) * albumScore;
                    embed.AddField(x => x
                        .WithName($"When you both like an artist, there's a {FormatPercent(albumMatchRatio)} chance you'll also like the same albums.")
                        .WithValue(ListFormat(albumMatches, FormatAlbumLink)));

                    if (albumScore != 0 && albumMatches.Count > 1 && GetCommonPlays(albumMatches) > 20)
                    {
                        var trackMatchRatio = (1 / albumScore) * trackScore;
                        embed.AddField(x => x
                            .WithName($"And you'll listen to the same songs on an album {FormatPercent(trackMatchRatio)} of the time.")
                            .WithValue(ListFormat(trackMatches, FormatTrackLink)));
                    }
                }
                
                var artistsResults = new
                {
                    first = (await artists.first),
                    second = (await artists.second)
                };

                bool TheyListenTo(LfScore<LfArtist> x) => x.Score > 0.005 || x.Playcount > 10;
                bool YouListenTo(LfScore<LfArtist> x) => x.Score > 0.01 || x.Playcount > 10;

                var onlyFirst = artistsResults.first.Where(x => !artistsResults.second.Any(y => x.Id.Equals(y.Id) && TheyListenTo(y)) && YouListenTo(x)).Take(3).ToList();
                embed.AddField(x => x
                    .WithName($"Only you listen to these artists:")
                    .WithValue(onlyFirst.Any() ? onlyFirst.Select(y => $"{FormatArtistLink(y.Entity, true)} ({FormatPercent(y.Score)})").WordJoin() : noData));

                var onlySecond = artistsResults.second.Where(x => !artistsResults.first.Any(y => x.Id.Equals(y.Id) && TheyListenTo(y)) && YouListenTo(x)).Take(3).ToList();
                embed.AddField(x => x
                    .WithName($"And only they listen to these:")
                    .WithValue(onlySecond.Any() ? onlySecond.Select(y => $"{FormatArtistLink(y.Entity, true)} ({FormatPercent(y.Score)})").WordJoin() : noData));

                await command.Message.Channel.SendMessageAsync(embed: embed.Build());
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "recent", "Shows user's recently played songs.", CommandFlags.TypingIndicator)]
        [Alias("lf", "rc")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmRecent(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var client = new LastFmClient(settings.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey);

                const int NumDisplayed = 100;
                var userInfoTask = client.GetUserInfo();
                var (results, nowPlaying) = await client.GetRecentTracks(count: NumDisplayed);

                if (!results.Any())
                    throw new AbortException("This user hasn't scrobbled anything recently.");

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var track in results.Take(NumDisplayed))
                {
                    string when = null;
                    if (nowPlaying)
                    {
                        nowPlaying = false;
                        when = "now playing";
                    }
                    else if (track.date?.uts != null)
                    {
                        var ts = TimeHelpers.UnixTimeStampToDateTimeOffset((double)track.date.uts) - DateTimeOffset.UtcNow;
                        when = ts.SimpleFormat();
                    }

                    pages.AppendLine($"`{place++}>` **{FormatDynamicLink(track, true)}** by **{FormatDynamicLink(track.artist, true)}**" + (when != null ? $"_ – {when}_" : string.Empty));
                }

                var userInfo = await userInfoTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(HelpBuilder.LfIconUrl)
                    .WithName($"{user} last listened to...");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    var embed = new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author);

                    if (userInfo?.playcount != null)
                        embed.WithFooter($"{userInfo.playcount} plays in total");

                    return embed;
                });

                await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "top", "artists", "Shows user's top artists.", CommandFlags.TypingIndicator)]
        [Alias("lf", "ta"), Alias("lf", "top", "artist"), Alias("lf", "artists")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmArtists(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LfStatsPeriod.Overall;
                var client = new LastFmClient(settings.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey);

                const int NumDisplayed = 100;
                var playcountTask = client.GetTotalPlaycount(period);
                var results = (await client.GetArtistScores(period, NumDisplayed, playcountTask)).ToList();
                if (!results.Any())
                    throw new AbortException("Looks like the user doesn't have any scrobbles in this time range...");

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var entry in results.Take(NumDisplayed))
                    pages.AppendLine($"`#{place++}` **{FormatArtistLink(entry.Entity, true)}**_ – {FormatPercent(entry.Score)} ({entry.Playcount} plays)_");

                var playsTotal = await playcountTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(HelpBuilder.LfIconUrl)
                    .WithName($"{user}'s top artists {FormatStatsPeriod(period)}");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    return new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author)
                        .WithFooter($"{playsTotal} plays in total");
                });

                await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "top", "albums", "Shows user's top albums.", CommandFlags.TypingIndicator)]
        [Alias("lf", "tal"), Alias("lf", "top", "album"), Alias("albums")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmAlbums(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LfStatsPeriod.Overall;
                var client = new LastFmClient(settings.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey);

                const int NumDisplayed = 100;
                var playcountTask = client.GetTotalPlaycount(period);
                var results = (await client.GetAlbumScores(period, NumDisplayed, playcountTask)).ToList();
                if (!results.Any())
                    throw new AbortException("Looks like the user doesn't have any scrobbles in this time range...");

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var entry in results.Take(NumDisplayed))
                    pages.AppendLine($"`#{place++}` **{FormatAlbumLink(entry.Entity, true)}** by **{FormatArtistLink(entry.Entity.Artist)}**_ – {FormatPercent(entry.Score)} ({entry.Playcount} plays)_");

                var playsTotal = await playcountTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(HelpBuilder.LfIconUrl)
                    .WithName($"{user}'s top albums {FormatStatsPeriod(period)}");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    return new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author)
                        .WithFooter($"{playsTotal} plays in total");
                });

                await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "top", "tracks", "Shows user's top tracks.", CommandFlags.TypingIndicator)]
        [Alias("lf", "tt"), Alias("lf", "top", "track"), Alias("lf", "tracks"), Alias("lf", "ts")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmTracks(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LfStatsPeriod.Overall;
                var client = new LastFmClient(settings.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey);

                const int NumDisplayed = 100;
                var playcountTask = client.GetTotalPlaycount(period);
                var results = (await client.GetTrackScores(period, NumDisplayed, playcountTask)).ToList();
                if (!results.Any())
                    throw new AbortException("Looks like the user doesn't have any scrobbles in this time range...");

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var entry in results.Take(NumDisplayed))
                    pages.AppendLine($"`#{place++}` **{FormatTrackLink(entry.Entity, true)}** by **{FormatArtistLink(entry.Entity.Artist, true)}**_ – {FormatPercent(entry.Score)} ({entry.Playcount} plays)_");

                var playsTotal = await playcountTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(HelpBuilder.LfIconUrl)
                    .WithName($"{user}'s top tracks {FormatStatsPeriod(period)}");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    return new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author)
                        .WithFooter($"{playsTotal} plays in total");
                });

                await command.Reply(Communicator, pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "artist", "Shows your favorite tracks and albums from a specific artist.", CommandFlags.TypingIndicator)]
        [Alias("lf", "ar")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional, "user (mention or ID); shows your own stats if omitted")]
        [Parameter("Artist", ParameterType.String, ParameterFlags.Remainder, "the artist's name on Last.fm (make sure you have the correct spelling)")]
        public async Task LastFmArtist(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author, command["User"].HasValue);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LfStatsPeriod.Overall;
                if (period == LfStatsPeriod.Day)
                    throw new IncorrectParametersCommandException("The `day` value can't be used with this command.", false);

                var client = new LastFmClient(settings.LastFmUsername, (await Settings.ReadGlobal<BotConfig>()).LastFmKey);

                const int NumDisplayed = 10;
                var info = await client.GetArtistDetail(command["Artist"], period);
                if (info == null)
                {
                    await command.ReplyError(Communicator, "Can't find this artist or user. Make sure you're using the same spelling as the artist's page on Last.fm.");
                    return;
                }

                var author = new EmbedAuthorBuilder()
                    .WithIconUrl(HelpBuilder.LfIconUrl)
                    .WithName($"{user}'s stats for {info.Name}");

                if (!settings.Anonymous)
                    author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                var description = new StringBuilder();
                description.AppendLine($"You've listened to this artist **{info.Playcount}** times.");
                description.AppendLine($"You've heard **{info.AlbumsListened}** of their albums and **{info.TracksListened}** of their tracks.");

                var embed = new EmbedBuilder()
                    .WithDescription(description.ToString())
                    .WithColor(0xd9, 0x23, 0x23)
                    .WithAuthor(author)
                    .WithFooter($"Based on {FormatStatsDataPeriod(period)}");

                if (info.Image != null)
                    embed.WithThumbnailUrl(info.Image.AbsoluteUri);

                if (info.TopAlbums.Any())
                {
                    var topList = new StringBuilder();
                    var place = 1;
                    foreach (var entry in info.TopAlbums.Take(NumDisplayed))
                        topList.TryAppendLineLimited($"`#{place++}` **{FormatAlbumLink(entry, true)}**_ – {entry.Playcount} plays_", DiscordHelpers.MaxEmbedFieldLength);
                    
                    embed.AddField("Top albums", topList.ToString());
                }

                if (info.TopTracks.Any())
                {
                    var topList = new StringBuilder();
                    var place = 1;
                    foreach (var entry in info.TopTracks.Take(NumDisplayed))
                        topList.TryAppendLineLimited($"`#{place++}` **{FormatTrackLink(entry, true)}**_ – {entry.Playcount} plays_", DiscordHelpers.MaxEmbedFieldLength);

                    embed.AddField("Top tracks", topList.ToString());
                }

                await command.Channel.SendMessageAsync(embed: embed.Build());
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                var extUser = command["User"].HasValue ? command["User"].AsGuildUserOrName.Item2 : null;
                throw new IncorrectParametersCommandException(extUser != null ? $"User `{extUser}` not found." : "User not found.");
            }
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach last.fm (error {(e.Response as HttpWebResponse)?.StatusCode}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "set", "Saves your username on Last.fm.", CommandFlags.DirectMessageAllow)]
        [Parameter("Username", ParameterType.String, ParameterFlags.Remainder, "your Last.fm username")]
        [Comment("Can be used in a direct message. Your username will be saved across servers.")]
        public async Task LastFmSet(ICommand command)
        {
            await Settings.ModifyUser(command.Message.Author.Id, (LastFmUserSettings x) =>
            {
                x.LastFmUsername = command["Username"];
                x.Anonymous = false;
            });

            await command.ReplySuccess(Communicator, $"Your Last.fm username has been set to `{command["Username"]}`.");
        }

        [Command("lf", "set", "anonymous", "Saves your username on Last.fm, without revealing it to others.", CommandFlags.DirectMessageAllow)]
        [Parameter("Username", ParameterType.String, ParameterFlags.Remainder, "your Last.fm username")]
        [Comment("Can be used in a direct message. Your username will be saved across servers.\nCommands won't include your username or link to your profile.")]
        public async Task LastFmSetAnonymous(ICommand command)
        {
            try
            {
                await command.Message.DeleteAsync();
            }
            catch (Discord.Net.HttpException)
            {
                //most likely missing permissions, ignore (no way to tell - discord often doesn't set the proper error code...)
            }

            await Settings.ModifyUser(command.Message.Author.Id, (LastFmUserSettings x) =>
            {
                x.LastFmUsername = command["Username"];
                x.Anonymous = true;
            });

            await command.ReplySuccess(Communicator, $"Your Last.fm username has been set.");
        }

        [Command("lf", "unset", "Deletes your saved Last.fm username.", CommandFlags.DirectMessageAllow)]
        [Comment("Can be used in a direct message.")]
        public async Task LastFmUnset(ICommand command)
        {
            await Settings.ModifyUser(command.Message.Author.Id, (LastFmUserSettings x) => x.LastFmUsername = null);
            await command.ReplySuccess(Communicator, $"Your Last.fm username has been deleted.");
        }

        string FormatDynamicLink(dynamic entity, bool trim = false)
            => FormatLink((string)entity?.name ?? "Unknown", (string)entity?.url, trim);

        string FormatArtistLink(LfArtist artist, bool trim = false) 
            => FormatLink(artist.Name ?? "Unknown", artist.Url, trim);

        string FormatAlbumLink(LfAlbum album, bool trim = false)
        {
            if (!string.IsNullOrEmpty(album.Url))
                return FormatLink(album.Name ?? "Unknown", album.Url, trim);
            else
                return FormatLink(album.Name ?? "Unknown", album.Artist.Url, trim); // Just for the formatting...
        }

        string FormatTrackLink(LfTrack track, bool trim = false)
            => FormatLink(track.Name ?? "Unknown", track.Url, trim);

        string FormatLink(string text, string url, bool trim = false)
            => DiscordHelpers.BuildMarkdownUri(text.Truncate(trim ? 22 : int.MaxValue), url);

        static string FormatPercent(double value)
        {
            value *= 100;
            if (value <= 0)
                return "0%";
            else if (value < 0.1)
                return $"<{(0.1).ToString("0.0", Definitions.GlobalDefinitions.Culture)}%";
            else if (value < 1)
                return $"{value.ToString("0.0", Definitions.GlobalDefinitions.Culture)}%";
            else
                return $"{value.ToString("F0", Definitions.GlobalDefinitions.Culture)}%";
        }

        async Task<(LastFmUserSettings settings, string name)> GetLastFmSettings(IGuildUser user, bool otherUser = false)
        {
            var settings = await Settings.ReadUser<LastFmUserSettings>(user.Id, false);
            if (settings == null || string.IsNullOrWhiteSpace(settings.LastFmUsername))
            {
                if (otherUser)
                    throw new AbortException($"This person hasn't set up their Last.fm username yet.");
                else
                    throw new AbortException($"You haven't set your Last.fm username yet.\nTo set it up, use the `lf set` command.\nIf you don't want to reveal your profile to others, use `lf set anonymous`.");
            }

            return (settings, user.Nickname ?? user.Username);
        }

        async Task<(LastFmUserSettings settings, string name)> GetLastFmSettings(ParameterToken param, IGuildUser fallback = null)
        {
            if (param.HasValue && !string.IsNullOrEmpty(param.AsGuildUserOrName.Item2))
            {
                // Off-discord username
                return (new LastFmUserSettings() { LastFmUsername = param.AsGuildUserOrName.Item2, Anonymous = false }, param.AsGuildUserOrName.Item2);
            }

            if (!param.HasValue && fallback == null)
                throw new ArgumentException();

            var user = param.HasValue ? param.AsGuildUserOrName.Item1 : fallback;
            return await GetLastFmSettings(user, param.HasValue);
        }

        LfStatsPeriod ParseStatsPeriod(string input) 
            => InputStatsPeriodMapping.TryGetValue(input, out var result) ? result : throw new IncorrectParametersCommandException("Invalid time period.");

        string FormatStatsPeriod(LfStatsPeriod period)
        {
            switch (period)
            {
                case LfStatsPeriod.Day: return "in the last 24 hours";
                case LfStatsPeriod.Week: return "from the last week";
                case LfStatsPeriod.Month: return "from the last month";
                case LfStatsPeriod.QuarterYear: return "from the last 3 months";
                case LfStatsPeriod.HalfYear: return "from the last 6 months";
                case LfStatsPeriod.Year: return "from the last year";
                case LfStatsPeriod.Overall: return "of all time";
                default: throw new ArgumentException($"Unknown value {period}");
            }
        }

        string FormatStatsDataPeriod(LfStatsPeriod period)
        {
            switch (period)
            {
                case LfStatsPeriod.Day: return "data from the last 24 hours";
                case LfStatsPeriod.Week: return "data from the last week";
                case LfStatsPeriod.Month: return "data from the last month";
                case LfStatsPeriod.QuarterYear: return "data from the last 3 months";
                case LfStatsPeriod.HalfYear: return "data from the last 6 months";
                case LfStatsPeriod.Year: return "data from the last year";
                case LfStatsPeriod.Overall: return "all data";
                default: throw new ArgumentException($"Unknown value {period}");
            }
        }

        static IEnumerable<LfMatch<T>> GetMatches<T>(IEnumerable<LfScore<T>> first, IEnumerable<LfScore<T>> second)
            where T : ILfEntity
        {
            var lookup = second.ToDictionary(x => x.Id);
            var result = new List<LfMatch<T>>();
            foreach (var entry in first)
            {
                if (lookup.TryGetValue(entry.Id, out var matched))
                    result.Add(new LfMatch<T>(entry.Entity, matched.Entity, Math.Min(entry.Score, matched.Score)));
            }

            return result.OrderByDescending(x => x.Score);
        }

        static int GetCommonPlays<T>(IEnumerable<LfMatch<T>> matches)
            where T : ILfEntity
            => matches.Aggregate(0, (x, y) => x + Math.Min(y.First.Playcount, y.Second.Playcount));
    }
}
