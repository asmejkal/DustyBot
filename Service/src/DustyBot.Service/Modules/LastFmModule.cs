using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using DustyBot.LastFm;
using DustyBot.LastFm.Models;
using DustyBot.Service.Configuration;
using DustyBot.Service.Definitions;
using DustyBot.Service.Helpers;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Modules
{
    [Module("Last.fm", "Show others what you're listening to.")]
    internal sealed class LastFmModule
    {
        private class LastFmMatch<T>
        {
            public double Score { get; }

            public T First { get; }
            public T Second { get; }

            internal LastFmMatch(T first, T second, double score)
            {
                First = first;
                Second = second;
                Score = score;
            }
        }

        private const string StatsPeriodRegex = "^(?:day|week|w|month|mo|3-?months|3-?month|3mo|6-?months|6-?month|6mo|year|y|all)$";

        private static readonly Dictionary<string, LastFmDataPeriod> InputStatsPeriodMapping =
            new Dictionary<string, LastFmDataPeriod>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "day", LastFmDataPeriod.Day },
            { "week", LastFmDataPeriod.Week },
            { "month", LastFmDataPeriod.Month },
            { "mo", LastFmDataPeriod.Month },
            { "3months", LastFmDataPeriod.QuarterYear },
            { "3month", LastFmDataPeriod.QuarterYear },
            { "3-months", LastFmDataPeriod.QuarterYear },
            { "3-month", LastFmDataPeriod.QuarterYear },
            { "3mo", LastFmDataPeriod.QuarterYear },
            { "6months", LastFmDataPeriod.HalfYear },
            { "6month", LastFmDataPeriod.HalfYear },
            { "6-months", LastFmDataPeriod.HalfYear },
            { "6-month", LastFmDataPeriod.HalfYear },
            { "6mo", LastFmDataPeriod.HalfYear },
            { "year", LastFmDataPeriod.Year },
            { "all", LastFmDataPeriod.Overall }
        };

        private readonly ILastFmSettingsService _settings;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly WebsiteWalker _websiteWalker;
        private readonly IOptions<IntegrationOptions> _integrationOptions;
        private readonly HelpBuilder _helpBuilder;

        public LastFmModule(
            ILastFmSettingsService settings, 
            IFrameworkReflector frameworkReflector, 
            WebsiteWalker websiteWalker,
            IOptions<IntegrationOptions> integrationOptions,
            HelpBuilder helpBuilder)
        {
            _settings = settings;
            _frameworkReflector = frameworkReflector;
            _websiteWalker = websiteWalker;
            _integrationOptions = integrationOptions;
            _helpBuilder = helpBuilder;
        }

        [Command("lf", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("lastfm"), Alias("lastfm", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("lf", "np", "Shows what song you or someone else is currently playing on Last.fm.", CommandFlags.TypingIndicator)]
        [Alias("lf"), Alias("np")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional | ParameterFlags.Remainder, "the user (mention, ID or Last.fm username); uses your Last.fm if omitted")]
        [Comment("Also shows the song's position among user's top 100 listened tracks.")]
        public async Task NowPlaying(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);

                var client = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);
                var topTracksTask = client.GetTopTracks(LastFmDataPeriod.Month, 100);
                var tracks = (await client.GetRecentTracks(count: 1)).ToList();

                if (!tracks.Any())
                {
                    await command.Reply(GetNoScrobblesMessage((await command["User"].AsGuildUserOrName)?.Item2));
                    return;
                }

                var nowPlaying = tracks[0].NowPlaying;
                var current = await client.GetTrackInfo(tracks[0].Artist.Name, tracks[0].Name);
                
                // Description
                var description = new StringBuilder();
                description.AppendLine($"**{FormatTrackLink(current ?? tracks[0].ToTrack())}** by **{FormatArtistLink(current?.Artist ?? tracks[0].Artist)}**");
                if (!string.IsNullOrEmpty(current?.Album?.Name))
                    description.AppendLine($"On {DiscordHelpers.BuildMarkdownUri(current.Album.Name, current.Album.Url)}");
                else if (!string.IsNullOrEmpty(tracks[0].Album?.Name))
                    description.AppendLine($"On {tracks[0].Album.Name}");

                var embed = new EmbedBuilder()
                    .WithDescription(description.ToString())
                    .WithColor(0xd9, 0x23, 0x23);

                // Image
                var imageUri = current?.Album?.ImageUri ?? tracks[0]?.Album?.ImageUri;
                if (imageUri != null)
                    embed.WithThumbnailUrl(imageUri.AbsoluteUri);

                // Title
                var author = new EmbedAuthorBuilder().WithIconUrl(_websiteWalker.LfIconUrl);
                if (!settings.Anonymous)
                    author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                if (nowPlaying)
                    author.WithName($"{user} is now listening to...");
                else
                    author.WithName($"{user} last listened to...");

                embed.WithAuthor(author);

                // Playcount
                var playCount = (current?.Playcount ?? 0) + (nowPlaying ? 1 : 0);
                if (playCount == 1 && current?.Playcount != null)
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
                            placementPlaycount = track.Playcount.Value;
                            placement = counter;
                        }

                        if (string.Compare(track.Url, (string)(current?.Url ?? tracks[0].Url), true) == 0)
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
                    embed.AddField(x => x.WithName("Previous").WithValue($"{FormatTrackLink(previous?.ToTrack())} by {FormatArtistLink(previous?.Artist)}"));
                }

                await command.Reply(embed.Build());
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "np", "spotify", "Searches and posts a Spotify link to what you're currently listening.", CommandFlags.TypingIndicator)]
        [Alias("np", "spotify"), Alias("lf", "spotify")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional | ParameterFlags.Remainder, "the user (mention, ID or Last.fm username); uses your Last.fm if omitted")]
        public async Task NowPlayingSpotify(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);

                var lfm = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);
                var nowPlayingTask = lfm.GetRecentTracks(count: 1);

                var spotify = await SpotifyClient.Create(_integrationOptions.Value.SpotifyId, _integrationOptions.Value.SpotifyKey);

                var nowPlaying = (await nowPlayingTask).FirstOrDefault();
                if (nowPlaying == null)
                {
                    await command.Reply(GetNoScrobblesMessage((await command["User"].AsGuildUserOrName)?.Item2));
                    return;
                }

                var url = await spotify.SearchTrackUrl($"{nowPlaying.Name} artist:{nowPlaying.Artist.Name}");
                if (string.IsNullOrEmpty(url))
                {
                    await command.Reply($"Can't find this track on Spotify...");
                    return;
                }

                await command.Reply($"<:sf:621852106235707392> **{user} is now listening to...**\n" + url);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
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
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LastFmDataPeriod.Year;

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
                        .WithIconUrl(_websiteWalker.LfIconUrl))
                        .WithDescription("What did you expect?")
                        .WithFooter($"Based on {FormatStatsDataPeriod(period)}");

                    await command.Reply(sameEmbed.Build());
                    return;
                }

                // Prepare data
                var clients = new
                {
                    first = new LastFmClient(settings.first.LastFmUsername, _integrationOptions.Value.LastFmKey),
                    second = new LastFmClient(settings.second.LastFmUsername, _integrationOptions.Value.LastFmKey)
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

                var artistMatches = GetMatches(await artists.first, await artists.second, x => x.Id).ToList();
                var artistScore = artistMatches.Aggregate(0.0, (x, y) => x + y.Score);

                var albumMatches = GetMatches(await albums.first, await albums.second, x => x.Id).ToList();
                var albumScore = albumMatches.Aggregate(0.0, (x, y) => x + y.Score);

                var trackMatches = GetMatches(await tracks.first, await tracks.second, x => x.Id).ToList();
                var trackScore = trackMatches.Aggregate(0.0, (x, y) => x + y.Score);

                // Listening to the same artist, but different albums -> 50% score
                // Listening to the same album, but different tracks -> 75% score
                // Listening to the same tracks -> 100% score
                var compatibility = Math.Pow(artistScore * 0.5 + albumScore * 0.25 + trackScore * 0.25, 0.4);

                var embed = new EmbedBuilder()
                    .WithAuthor(x => x.WithName($"Your music taste is {FormatPercent(compatibility)} compatible!")
                    .WithIconUrl(_websiteWalker.LfIconUrl))
                    .WithFooter($"Based on {FormatStatsDataPeriod(period)}")
                    .WithColor(0xd9, 0x23, 0x23);

                if (settings.second.UserId != default)
                    embed.WithDescription($"Based on how often you and <@{settings.second.UserId}> listen to the same music.");
                else
                    embed.WithDescription($"Based on how often you listen to the same music.");

                const string noData = "Nothing to show here";
                string ListFormat<T>(List<LastFmMatch<T>> data, Func<T, bool, string> formatter)
                {
                    if (data.Any())
                        return data.Take(3).Select(y => $"{formatter(y.First, true)} ({FormatPercent(y.Score)})").WordJoin(lastSeparator: " and ") + ".";
                    else
                        return noData;
                }

                embed.AddField(x => x
                    .WithName($"You listen to the same artists {FormatPercent(artistScore)} of the time.")
                    .WithValue(ListFormat(artistMatches, FormatArtistLink)));

                // Only show more stats if we have sufficient data
                if (artistScore != 0 && artistMatches.Count > 1 && GetCommonPlays(artistMatches, x => x.Playcount.Value) > 20)
                {
                    var albumMatchRatio = (1 / artistScore) * albumScore;
                    embed.AddField(x => x
                        .WithName($"When you both like an artist, there's a {FormatPercent(albumMatchRatio)} chance you'll like the same albums.")
                        .WithValue(ListFormat(albumMatches, FormatAlbumLink)));

                    if (albumScore != 0 && albumMatches.Count > 1 && GetCommonPlays(albumMatches, x => x.Playcount.Value) > 20)
                    {
                        var trackMatchRatio = (1 / albumScore) * trackScore;
                        embed.AddField(x => x
                            .WithName($"And you'll listen to the same songs on an album {FormatPercent(trackMatchRatio)} of the time.")
                            .WithValue(ListFormat(trackMatches, FormatTrackLink)));
                    }
                }
                
                var artistsResults = new
                {
                    first = await artists.first,
                    second = await artists.second
                };

                bool TheyListenTo(LastFmScore<LastFmArtist> x) => x.Score > 0.005 || x.Entity.Playcount > 10;
                bool YouListenTo(LastFmScore<LastFmArtist> x) => x.Score > 0.01 || x.Entity.Playcount > 10;

                var onlyFirst = artistsResults.first.Where(x => !artistsResults.second.Any(y => x.Entity.Id.Equals(y.Entity.Id) && TheyListenTo(y)) && YouListenTo(x)).Take(3).ToList();
                embed.AddField(x => x
                    .WithName($"Only you listen to these artists:")
                    .WithValue(onlyFirst.Any() ? onlyFirst.Select(y => $"{FormatArtistLink(y.Entity, true)} ({FormatPercent(y.Score)})").WordJoin(lastSeparator: " and ") : noData));

                var onlySecond = artistsResults.second.Where(x => !artistsResults.first.Any(y => x.Entity.Id.Equals(y.Entity.Id) && TheyListenTo(y)) && YouListenTo(x)).Take(3).ToList();
                embed.AddField(x => x
                    .WithName($"And only they listen to these:")
                    .WithValue(onlySecond.Any() ? onlySecond.Select(y => $"{FormatArtistLink(y.Entity, true)} ({FormatPercent(y.Score)})").WordJoin(lastSeparator: " and ") : noData));

                await command.Reply(embed.Build());
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
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
                var client = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);

                const int NumDisplayed = 100;
                var userInfoTask = client.GetUserInfo();
                var results = await client.GetRecentTracks(count: NumDisplayed);

                if (!results.Any())
                    throw new AbortException("This user hasn't scrobbled anything recently.");

                var nowPlaying = results.First().NowPlaying;
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
                    else if (track.Timestamp.HasValue)
                    {
                        when = (track.Timestamp.Value - DateTimeOffset.UtcNow).SimpleFormat();
                    }

                    pages.AppendLine($"`{place++}>` **{FormatTrackLink(track.ToTrack(), true)}** by **{FormatArtistLink(track.Artist, true)}**" + (when != null ? $"_ – {when}_" : string.Empty));
                }

                var userInfo = await userInfoTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                        .WithIconUrl(_websiteWalker.LfIconUrl)
                        .WithName($"{user} last listened to...");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    var embed = new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author);

                    if (userInfo?.Playcount != null)
                        embed.WithFooter($"{userInfo.Playcount} plays in total");

                    return embed;
                });

                await command.Reply(pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "top", "artists", "Shows user's top artists.", CommandFlags.TypingIndicator)]
        [Alias("lf", "ta"), Alias("lf", "top", "artist", true), Alias("lf", "topartists", true), Alias("lf", "topartist", true), Alias("lf", "artists")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmArtists(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LastFmDataPeriod.Overall;
                var client = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);

                const int NumDisplayed = 100;
                var playcountTask = client.GetTotalPlaycount(period);
                var results = (await client.GetArtistScores(period, NumDisplayed, playcountTask)).ToList();
                if (!results.Any())
                    throw new AbortException(GetNoScrobblesTimePeriodMessage((await command["User"].AsGuildUserOrName)?.Item2));

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var entry in results.Take(NumDisplayed))
                    pages.AppendLine($"`#{place++}` **{FormatArtistLink(entry.Entity, true)}**_ – {FormatPercent(entry.Score)} ({entry.Entity.Playcount} plays)_");

                var playsTotal = await playcountTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(_websiteWalker.LfIconUrl)
                    .WithName($"{user}'s top artists {FormatStatsPeriod(period)}");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    return new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author)
                        .WithFooter($"{playsTotal} plays in total");
                });

                await command.Reply(pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "top", "albums", "Shows user's top albums.", CommandFlags.TypingIndicator)]
        [Alias("lf", "tal"), Alias("lf", "top", "album", true), Alias("lf", "topalbums", true), Alias("lf", "topalbum", true), Alias("albums")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmAlbums(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LastFmDataPeriod.Overall;
                var client = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);

                const int NumDisplayed = 100;
                var playcountTask = client.GetTotalPlaycount(period);
                var results = (await client.GetAlbumScores(period, NumDisplayed, playcountTask)).ToList();
                if (!results.Any())
                    throw new AbortException(GetNoScrobblesTimePeriodMessage((await command["User"].AsGuildUserOrName)?.Item2));

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var entry in results.Take(NumDisplayed))
                    pages.AppendLine($"`#{place++}` **{FormatAlbumLink(entry.Entity, true)}** by **{FormatArtistLink(entry.Entity.Artist)}**_ – {FormatPercent(entry.Score)} ({entry.Entity.Playcount} plays)_");

                var playsTotal = await playcountTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(_websiteWalker.LfIconUrl)
                    .WithName($"{user}'s top albums {FormatStatsPeriod(period)}");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    return new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author)
                        .WithFooter($"{playsTotal} plays in total");
                });

                await command.Reply(pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "top", "tracks", "Shows user's top tracks.", CommandFlags.TypingIndicator)]
        [Alias("lf", "tt"), Alias("lf", "top", "track", true), Alias("lf", "toptracks", true), Alias("lf", "toptrack", true), Alias("lf", "tracks"), Alias("lf", "ts")]
        [Parameter("Period", StatsPeriodRegex, ParameterType.Regex, ParameterFlags.Optional, "how far back to check; available values are `day`, `week`, `month`, `3months`, `6months`, `year`, and `all` (default)")]
        [Parameter("User", ParameterType.GuildUserOrName, ParameterFlags.Optional, "the user (mention, ID or Last.fm username); shows your own stats if omitted")]
        public async Task LastFmTracks(ICommand command)
        {
            try
            {
                var (settings, user) = await GetLastFmSettings(command["User"], (IGuildUser)command.Author);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LastFmDataPeriod.Overall;
                var client = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);

                const int NumDisplayed = 100;
                var playcountTask = client.GetTotalPlaycount(period);
                var results = (await client.GetTrackScores(period, NumDisplayed, playcountTask)).ToList();
                if (!results.Any())
                    throw new AbortException(GetNoScrobblesTimePeriodMessage((await command["User"].AsGuildUserOrName)?.Item2));

                var pages = new PageCollectionBuilder();
                var place = 1;
                foreach (var entry in results.Take(NumDisplayed))
                    pages.AppendLine($"`#{place++}` **{FormatTrackLink(entry.Entity, true)}** by **{FormatArtistLink(entry.Entity.Artist, true)}**_ – {FormatPercent(entry.Score)} ({entry.Entity.Playcount} plays)_");

                var playsTotal = await playcountTask;
                var embedFactory = new Func<EmbedBuilder>(() =>
                {
                    var author = new EmbedAuthorBuilder()
                    .WithIconUrl(_websiteWalker.LfIconUrl)
                    .WithName($"{user}'s top tracks {FormatStatsPeriod(period)}");

                    if (!settings.Anonymous)
                        author.WithUrl($"https://www.last.fm/user/{settings.LastFmUsername}");

                    return new EmbedBuilder()
                        .WithColor(0xd9, 0x23, 0x23)
                        .WithAuthor(author)
                        .WithFooter($"{playsTotal} plays in total");
                });

                await command.Reply(pages.BuildEmbedCollection(embedFactory, 10), true);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
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
                var (settings, user) = await GetLastFmSettings(command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author, command["User"].HasValue);
                var period = command["Period"].HasValue ? ParseStatsPeriod(command["Period"]) : LastFmDataPeriod.Overall;
                if (period == LastFmDataPeriod.Day)
                    throw new IncorrectParametersCommandException("The `day` value can't be used with this command.", false);

                var client = new LastFmClient(settings.LastFmUsername, _integrationOptions.Value.LastFmKey);

                const int NumDisplayed = 10;
                var info = await client.GetArtistDetail(command["Artist"], period);
                if (info == null)
                {
                    await command.ReplyError("Can't find this artist or user. Make sure you're using the same spelling as the artist's page on Last.fm.");
                    return;
                }

                var author = new EmbedAuthorBuilder()
                    .WithIconUrl(_websiteWalker.LfIconUrl)
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

                if (info.ImageUri != null)
                    embed.WithThumbnailUrl(info.ImageUri.AbsoluteUri);

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

                await command.Reply(embed.Build());
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                ThrowUserNotFound((await command["User"].AsGuildUserOrName)?.Item2);
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CommandException("The bot can't access your recently listened tracks. \n\nPlease make sure you don't have `Hide recent listening information` checked in your Last.fm settings (Settings -> Privacy -> Recent listening).");
            }
            catch (WebException e)
            {
                await command.Reply($"Last.fm is down (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }

        [Command("lf", "set", "Saves your username on Last.fm.", CommandFlags.DirectMessageAllow)]
        [Parameter("Username", ParameterType.String, ParameterFlags.Remainder, "your Last.fm username")]
        [Comment("Can be used in a direct message. Your username will be saved across servers.")]
        public async Task LastFmSet(ICommand command, CancellationToken ct)
        {
            await _settings.SetUsernameAsync(command.Message.Author.Id, command["Username"], false, ct);
            await command.ReplySuccess($"Your Last.fm username has been set to `{command["Username"]}`.");
        }

        [Command("lf", "set", "anonymous", "Saves your username on Last.fm, without revealing it to others.", CommandFlags.DirectMessageAllow)]
        [Parameter("Username", ParameterType.String, ParameterFlags.Remainder, "your Last.fm username")]
        [Comment("Can be used in a direct message. Your username will be saved across servers.\nCommands won't include your username or link to your profile.")]
        public async Task LastFmSetAnonymous(ICommand command, CancellationToken ct)
        {
            try
            {
                if ((await command.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel)command.Channel).ManageMessages)
                    await command.Message.DeleteAsync();
            }
            catch (Discord.Net.HttpException)
            {
                // Ignore
            }

            await _settings.SetUsernameAsync(command.Message.Author.Id, command["Username"], true, ct);
            await command.ReplySuccess($"Your Last.fm username has been set.");
        }

        [Command("lf", "reset", "Deletes your saved Last.fm username.", CommandFlags.DirectMessageAllow)]
        [Alias("lf", "unset", true)]
        [Comment("Can be used in a direct message.")]
        public async Task LastFmUnset(ICommand command, CancellationToken ct)
        {
            await _settings.ResetAsync(command.Message.Author.Id, ct);
            await command.ReplySuccess($"Your Last.fm username has been deleted.");
        }

        private string FormatArtistLink(LastFmArtist artist, bool trim = false) 
            => FormatLink(artist?.Name ?? "Unknown", artist?.Url, trim);

        private string FormatAlbumLink(LastFmAlbum album, bool trim = false)
        {
            if (!string.IsNullOrEmpty(album?.Url))
                return FormatLink(album?.Name ?? "Unknown", album?.Url, trim);
            else
                return FormatLink(album?.Name ?? "Unknown", album?.Artist?.Url, trim); // Just for the formatting...
        }

        private string FormatTrackLink(LastFmTrack track, bool trim = false)
            => FormatLink(track?.Name ?? "Unknown", track?.Url, trim);

        private string FormatLink(string text, string url, bool trim = false)
            => DiscordHelpers.BuildMarkdownUri(text.Truncate(trim ? 22 : int.MaxValue), url);

        private static string FormatPercent(double value)
        {
            value *= 100;
            if (value <= 0)
                return "0%";
            else if (value < 0.1)
                return $"<{0.1.ToString("0.0", GlobalDefinitions.Culture)}%";
            else if (value < 1)
                return $"{value.ToString("0.0", GlobalDefinitions.Culture)}%";
            else
                return $"{value.ToString("F0", GlobalDefinitions.Culture)}%";
        }

        private async Task<(LastFmUserSettings settings, string name)> GetLastFmSettings(IGuildUser user, bool otherUser = false)
        {
            var settings = await _settings.ReadAsync(user.Id);
            if (settings == null || string.IsNullOrWhiteSpace(settings.LastFmUsername))
            {
                if (otherUser)
                    throw new AbortException($"This person hasn't set up their Last.fm username yet.");
                else
                    throw new AbortException($"You haven't set your Last.fm username yet.\nTo set it up, use the `lf set` command.\nIf you don't want to reveal your profile to others, use `lf set anonymous`.");
            }

            return (settings, user.Nickname ?? user.Username);
        }

        private async Task<(LastFmUserSettings settings, string name)> GetLastFmSettings(ParameterToken param, IGuildUser fallback = null)
        {
            var userOrName = await param.AsGuildUserOrName;
            if (!string.IsNullOrEmpty(userOrName?.Item2))
            {
                // Off-discord username
                return (new LastFmUserSettings() { LastFmUsername = userOrName.Item2, Anonymous = false }, userOrName.Item2);
            }

            if (userOrName == null && fallback == null)
                throw new ArgumentException();

            var user = param.HasValue ? userOrName.Item1 : fallback;
            return await GetLastFmSettings(user, param.HasValue);
        }

        private static string GetNoScrobblesMessage(string offDiscordUser) =>
            $"Looks like {(!string.IsNullOrEmpty(offDiscordUser) ? $"user `{offDiscordUser}`" : "this user")} doesn't have any scrobbles yet...";

        private static string GetNoScrobblesTimePeriodMessage(string offDiscordUser) =>
            $"Looks like {(!string.IsNullOrEmpty(offDiscordUser) ? $"user `{offDiscordUser}`" : "this user")} doesn't have any scrobbles in this time period...";

        private LastFmDataPeriod ParseStatsPeriod(string input) 
            => InputStatsPeriodMapping.TryGetValue(input, out var result) ? result : throw new IncorrectParametersCommandException("Invalid time period.");

        private string FormatStatsPeriod(LastFmDataPeriod period)
        {
            switch (period)
            {
                case LastFmDataPeriod.Day: return "in the last 24 hours";
                case LastFmDataPeriod.Week: return "from the last week";
                case LastFmDataPeriod.Month: return "from the last month";
                case LastFmDataPeriod.QuarterYear: return "from the last 3 months";
                case LastFmDataPeriod.HalfYear: return "from the last 6 months";
                case LastFmDataPeriod.Year: return "from the last year";
                case LastFmDataPeriod.Overall: return "of all time";
                default: throw new ArgumentException($"Unknown value {period}");
            }
        }

        private string FormatStatsDataPeriod(LastFmDataPeriod period)
        {
            switch (period)
            {
                case LastFmDataPeriod.Day: return "data from the last 24 hours";
                case LastFmDataPeriod.Week: return "data from the last week";
                case LastFmDataPeriod.Month: return "data from the last month";
                case LastFmDataPeriod.QuarterYear: return "data from the last 3 months";
                case LastFmDataPeriod.HalfYear: return "data from the last 6 months";
                case LastFmDataPeriod.Year: return "data from the last year";
                case LastFmDataPeriod.Overall: return "all data";
                default: throw new ArgumentException($"Unknown value {period}");
            }
        }

        private static IEnumerable<LastFmMatch<T>> GetMatches<T>(IEnumerable<LastFmScore<T>> first, IEnumerable<LastFmScore<T>> second, Func<T, object> keySelector)
        {
            var lookup = second.ToDictionary(x => keySelector(x.Entity));
            var result = new List<LastFmMatch<T>>();
            foreach (var entry in first)
            {
                if (lookup.TryGetValue(keySelector(entry.Entity), out var matched))
                    result.Add(new LastFmMatch<T>(entry.Entity, matched.Entity, Math.Min(entry.Score, matched.Score)));
            }

            return result.OrderByDescending(x => x.Score);
        }

        private static int GetCommonPlays<T>(IEnumerable<LastFmMatch<T>> matches, Func<T, int> playcountSelector)
            => matches.Aggregate(0, (x, y) => x + Math.Min(playcountSelector(y.First), playcountSelector(y.Second)));

        private static void ThrowUserNotFound(string name) => 
            throw new IncorrectParametersCommandException(name != null ? $"User `{name}` not found." : "User not found.");
    }
}
