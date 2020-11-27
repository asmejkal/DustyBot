using DustyBot.LastFm.Models;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DustyBot.LastFm
{
    public class LastFmClient
    {
        public const int MaxRecentTracksPageSize = 1000; // Docs say 200, but it's actually 1000
        public const int MaxTopPageSize = 1000;

        private const string ApiBase = "http://ws.audioscrobbler.com/2.0";
        private const string CatalogueUrlPrefix = "https://www.last.fm/music/";

        private static readonly IReadOnlyDictionary<LastFmDataPeriod, string> StatsPeriodMapping = new Dictionary<LastFmDataPeriod, string>()
        {
            { LastFmDataPeriod.Overall, "overall" },
            { LastFmDataPeriod.Week, "7day" },
            { LastFmDataPeriod.Month, "1month" },
            { LastFmDataPeriod.QuarterYear, "3month" },
            { LastFmDataPeriod.HalfYear, "6month" },
            { LastFmDataPeriod.Year, "12month" },
        };

        private static readonly IReadOnlyDictionary<LastFmDataPeriod, string> StatsPeriodWebMapping = new Dictionary<LastFmDataPeriod, string>()
        {
            { LastFmDataPeriod.Overall, "ALL" },
            { LastFmDataPeriod.Week, "LAST_7_DAYS" },
            { LastFmDataPeriod.Month, "LAST_30_DAYS" },
            { LastFmDataPeriod.QuarterYear, "LAST_90_DAYS" },
            { LastFmDataPeriod.HalfYear, "LAST_180_DAYS" },
            { LastFmDataPeriod.Year, "LAST_365_DAYS" },
        };

        // Same values as on Last.fm's website
        private static readonly IReadOnlyDictionary<LastFmDataPeriod, TimeSpan> StatsPeriodTimeMapping = new Dictionary<LastFmDataPeriod, TimeSpan>()
        {
            { LastFmDataPeriod.Day, TimeSpan.FromDays(1) },
            { LastFmDataPeriod.Week, TimeSpan.FromDays(7) },
            { LastFmDataPeriod.Month, TimeSpan.FromDays(30) },
            { LastFmDataPeriod.QuarterYear, TimeSpan.FromDays(90) },
            { LastFmDataPeriod.HalfYear, TimeSpan.FromDays(180) },
            { LastFmDataPeriod.Year, TimeSpan.FromDays(365) },
        };

        public string User { get; }
        private string Key { get; }

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        public LastFmClient(string user, string key)
        {
            User = user;
            Key = key;
        }

        public async Task<LastFmUserInfo> GetUserInfo()
        {
            var request = WebRequest.CreateHttp($"{ApiBase}/?method=user.getinfo&user={User}&api_key={Key}&format=json");
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                var user = JObject.Parse(text)["user"];

                return new LastFmUserInfo((int?)user["playcount"] ?? 0);
            }
        }

        public Task<IEnumerable<LastFmRecentTrack>> GetRecentTracks(LastFmDataPeriod period, int count = int.MaxValue)
            => period == LastFmDataPeriod.Overall ? GetRecentTracks(count: count) : GetRecentTracks(StatsPeriodTimeMapping[period], count);

        public Task<IEnumerable<LastFmRecentTrack>> GetRecentTracks(TimeSpan period, int count = int.MaxValue)
            => GetRecentTracks(DateTimeOffset.UtcNow - period, count);

        public async Task<IEnumerable<LastFmRecentTrack>> GetRecentTracks(DateTimeOffset? from = null, int count = int.MaxValue)
        {
            var results = Enumerable.Empty<LastFmRecentTrack>();
            await foreach (var page in GetRecentTrackPages(from, count))
                results = results.Concat(page);

            return results;
        }

        public async IAsyncEnumerable<IReadOnlyCollection<LastFmRecentTrack>> GetRecentTrackPages(DateTimeOffset? from = null, int count = int.MaxValue)
        {
            var retrieved = 0;
            var page = 1;
            while (retrieved < count)
            {
                var (result, remaining) = await GetRecentTracksPage(page, Math.Min(count, MaxRecentTracksPageSize), from);
                yield return result;

                retrieved += Math.Min(count, MaxRecentTracksPageSize);
                if (!remaining)
                    break;

                page++;
            }
        }

        public async Task<(IReadOnlyCollection<LastFmRecentTrack> tracks, bool remaining)> GetRecentTracksPage(int page, int count, DateTimeOffset? from = null)
        {
            var request = WebRequest.CreateHttp($"{ApiBase}/?method=user.getrecenttracks&user={User}&api_key={Key}&format=json&limit={count}&page={page}&extended=1" + (from.HasValue ? $"&from={from.Value.ToUnixTimeSeconds()}" : string.Empty));
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                var root = JObject.Parse(text);
                var tracks = root["recenttracks"]?["track"] as JArray;

                if (tracks != null && tracks.Any())
                {
                    var nowPlaying = page == 1 && string.Compare((string)tracks[0]["@attr"]?["nowplaying"], "true", true) == 0;
                    var result = tracks.Select(x =>
                    {
                        var uts = (long?)x["date"]?["uts"];
                        return new LastFmRecentTrack(
                            (string)x["name"],
                            nowPlaying, 
                            (string)x["artist"]?["name"], 
                            (string)x["album"]?["#text"],
                            GetLargestImage(x["image"]),
                            uts.HasValue ? DateTimeOffset.FromUnixTimeSeconds(uts.Value) : (DateTimeOffset?)null);
                    });
                    
                    var more = page < (int)root["recenttracks"]["@attr"]["totalPages"];
                    return (result.ToList(), more);
                }
                else
                    return (Array.Empty<LastFmRecentTrack>(), false);
            }
        }

        public async Task<LastFmArtist> GetArtistInfo(string name)
        {
            var request = WebRequest.CreateHttp($"{ApiBase}/?method=artist.getInfo&api_key={Key}&artist={Uri.EscapeDataString(name)}&format=json&username={User}");
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                var artist = JObject.Parse(text)?["artist"];
                if (artist == null)
                    return null;

                return new LastFmArtist((string)artist["name"], imageUri: GetLargestImage(artist["image"]));
            }
        }

        public async Task<LastFmTrack> GetTrackInfo(string artistName, string name)
        {
            var request = WebRequest.CreateHttp($"{ApiBase}/?method=track.getInfo&api_key={Key}&artist={Uri.EscapeDataString(artistName)}&track={Uri.EscapeDataString(name)}&format=json&username={User}");
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                var track = JObject.Parse(text)?["track"];
                if (track == null)
                    return null;

                var artist = new LastFmArtist((string)track["artist"]["name"]);
                var album = new LastFmAlbum((string)track["album"]?["title"], artist, imageUri: GetLargestImage(track["album"]?["image"]));
                return new LastFmTrack((string)track["name"], album, (int?)track["userplaycount"]);
            }
        }

        public async Task<IEnumerable<LastFmArtist>> GetTopArtists(LastFmDataPeriod period, int count)
        {
            if (period == LastFmDataPeriod.Day)
            {
                var tracks = await GetRecentTracks(StatsPeriodTimeMapping[period]);
                return tracks.SkipWhile(x => x.NowPlaying).GroupBy(x => x.Artist.Id).Select(x => x.First().Artist.WithPlaycount(x.Count()));
            }
            else
            {
                var request = WebRequest.CreateHttp($"{ApiBase}/?method=user.gettopartists&user={User}&api_key={Key}&period={StatsPeriodMapping[period]}&limit={count}&format=json");
                request.Timeout = (int)RequestTimeout.TotalMilliseconds;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var root = JObject.Parse(text);
                    var results = root?["topartists"]?["artist"] as JArray ?? Enumerable.Empty<JToken>();
                    return results.Select(x => new LastFmArtist((string)x["name"], (int)x["playcount"]));
                }
            }
        }

        public async Task<IEnumerable<LastFmAlbum>> GetTopAlbums(LastFmDataPeriod period, int count)
        {
            if (period == LastFmDataPeriod.Day)
            {
                var tracks = await GetRecentTracks(StatsPeriodTimeMapping[period]);
                return tracks.SkipWhile(x => x.NowPlaying).GroupBy(x => x.Album.Id).Select(x => x.First().Album.WithPlaycount(x.Count()));
            }
            else
            {
                var request = WebRequest.CreateHttp($"{ApiBase}/?method=user.gettopalbums&user={User}&api_key={Key}&period={StatsPeriodMapping[period]}&limit={count}&format=json");
                request.Timeout = (int)RequestTimeout.TotalMilliseconds;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    var root = JObject.Parse(text);
                    var results = root?["topalbums"]?["album"] as JArray ?? Enumerable.Empty<JToken>();
                    return results.Select(x => new LastFmAlbum((string)x["name"], new LastFmArtist((string)x["artist"]["name"]), (int)x["playcount"]));
                }
            }
        }

        public async Task<IEnumerable<LastFmTrack>> GetTopTracks(LastFmDataPeriod period, int count = int.MaxValue, CancellationToken ct = default)
        {
            if (period == LastFmDataPeriod.Day)
            {
                var tracks = await GetRecentTracks(StatsPeriodTimeMapping[period]);
                return tracks.SkipWhile(x => x.NowPlaying).GroupBy(x => x.Id).Select(x => x.First().ToTrack(x.Count()));
            }
            else
            {
                var retrieved = 0;
                var page = 1;
                var results = Enumerable.Empty<LastFmTrack>();
                var pageSize = Math.Min(count, MaxTopPageSize);
                while (retrieved < count)
                {
                    ct.ThrowIfCancellationRequested();
                    var request = WebRequest.CreateHttp($"{ApiBase}/?method=user.gettoptracks&user={User}&api_key={Key}&period={StatsPeriodMapping[period]}&limit={pageSize}&page={page}&format=json");
                    request.Timeout = (int)RequestTimeout.TotalMilliseconds;
                    using (var response = (HttpWebResponse)await request.GetResponseAsync())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        ct.ThrowIfCancellationRequested();
                        var text = await reader.ReadToEndAsync();
                        ct.ThrowIfCancellationRequested();

                        var root = JObject.Parse(text);
                        var pageResults = (root?["toptracks"]?["track"] as JArray ?? Enumerable.Empty<JToken>()).ToList();
                        results = results.Concat(pageResults.Select(x => new LastFmTrack((string)x["name"], new LastFmAlbum(null, new LastFmArtist((string)x["artist"]["name"]), null), (int)x["playcount"])));

                        retrieved += pageResults.Count;
                        var more = page < ((int?)root?["toptracks"]?["@attr"]?["totalPages"] ?? 1);
                        if (!more || !pageResults.Any())
                            break;
                    }

                    page++;
                }

                return results;
            }
        }

        public Task<int> GetTotalPlaycount(LastFmDataPeriod period) 
            => period == LastFmDataPeriod.Overall ? GetTotalPlaycount() : GetTotalPlaycount(StatsPeriodTimeMapping[period]);

        public Task<int> GetTotalPlaycount(TimeSpan period)
            => GetTotalPlaycount(DateTimeOffset.UtcNow - period);

        public async Task<int> GetTotalPlaycount(DateTimeOffset? from = null)
        {
            var request = WebRequest.CreateHttp($"{ApiBase}/?method=user.getrecenttracks&user={User}&api_key={Key}&limit=1&format=json" + (from.HasValue ? $"&from={from.Value.ToUnixTimeSeconds()}" : string.Empty));
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                var root = JObject.Parse(text);
                return (int)root["recenttracks"]["@attr"]["total"];
            }
        }

        public async Task<LastFmArtistDetail> GetArtistDetail(string artist, LastFmDataPeriod period)
        {
            try
            {
                var infoTask = GetArtistInfo(artist);
                var request = WebRequest.CreateHttp($"https://www.last.fm/user/{User}/library/music/{Uri.EscapeDataString(artist)}?date_preset={StatsPeriodWebMapping[period]}");
                request.Timeout = (int)RequestTimeout.TotalMilliseconds;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var content = await reader.ReadToEndAsync();

                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    // Get artist info
                    var info = await infoTask;
                    if (info == null)
                        return null;

                    // Get avatar (we can't get it via the API because it never actually has the artist's image...)
                    var image = doc.DocumentNode.Descendants("span")
                        .FirstOrDefault(x => x.HasClass("library-header-image"))?
                        .Descendants("img")
                        .FirstOrDefault()?
                        .GetAttributeValue("src", null)?
                        .Replace("avatar70s", "avatar170s");

                    // Get playcount and count of listened albums and tracks (in that order)
                    var metadata = doc.DocumentNode
                        .Descendants("p")
                        .Where(x => x.HasClass("metadata-display") && x.ParentNode.HasClass("metadata-item"))
                        .Select(x => int.Parse(x.InnerText, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("en-US")))
                        .ToList();

                    var playcount = metadata.ElementAtOrDefault(0);
                    var albumsListened = metadata.ElementAtOrDefault(1);
                    var tracksListened = metadata.ElementAtOrDefault(2);

                    // Get top albums and tracks
                    IEnumerable<(string name, string url, int playcount)> GetTopListItems(HtmlNode topList)
                    {
                        foreach (var item in topList.Descendants("tr").Where(x => x.HasClass("chartlist-row") || x.HasClass("chartlist-row\n")))
                        {
                            var itemNameLink = item.Descendants("td").First(x => x.HasClass("chartlist-name")).Descendants("a").First();
                            var itemUrl = "https://www.last.fm" + itemNameLink.GetAttributeValue("href", null);

                            var scrobbleText = item.Descendants("span").First(x => x.HasClass("chartlist-count-bar-value")).InnerText;
                            var scrobbleCount = int.Parse(Regex.Match(scrobbleText, @"[\d,.\s]+").Value, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("en-US"));
                            yield return (WebUtility.HtmlDecode(itemNameLink.InnerText), itemUrl, scrobbleCount);
                        }
                    }

                    var topLists = doc.DocumentNode.Descendants("table").Where(x => x.HasClass("chartlist") || x.HasClass("chartlist\n")).ToList();
                    var topAlbums = Enumerable.Empty<LastFmAlbum>();
                    if (topLists.Any())
                        topAlbums = GetTopListItems(topLists.First()).Select(x => new LastFmAlbum(x.name, info, x.playcount));

                    var topTracks = Enumerable.Empty<LastFmTrack>();
                    if (topLists.Skip(1).Any())
                        topTracks = GetTopListItems(topLists.Skip(1).First()).Select(x => new LastFmTrack(x.name, new LastFmAlbum(null, info), x.playcount));

                    var imageUri = string.IsNullOrEmpty(image) ? null : new Uri(image);
                    return new LastFmArtistDetail(info, imageUri, topAlbums, topTracks, albumsListened, tracksListened, playcount);
                }
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<IEnumerable<LastFmScore<LastFmArtist>>> GetArtistScores(LastFmDataPeriod period, int count, Task<int> totalPlaycount)
        {
            var items = await GetTopArtists(period, count);
            var playcount = await totalPlaycount;
            return items.Select(x => new LastFmScore<LastFmArtist>(x, (double)x.Playcount / playcount));
        }

        public async Task<IEnumerable<LastFmScore<LastFmAlbum>>> GetAlbumScores(LastFmDataPeriod period, int count, Task<int> totalPlaycount)
        {
            var items = await GetTopAlbums(period, count);
            var playcount = await totalPlaycount;
            return items.Select(x => new LastFmScore<LastFmAlbum>(x, (double)x.Playcount / playcount));
        }

        public async Task<IEnumerable<LastFmScore<LastFmTrack>>> GetTrackScores(LastFmDataPeriod period, int count, Task<int> totalPlaycount)
        {
            var items = await GetTopTracks(period, count);
            var playcount = await totalPlaycount;
            return items.Select(x => new LastFmScore<LastFmTrack>(x, (double)x.Playcount / playcount));
        }

        public static string GetArtistUrl(string artist) => CatalogueUrlPrefix + GetArtistUrlPath(artist);
        public static string GetAlbumUrl(string artist, string album) => CatalogueUrlPrefix + GetAlbumUrlPath(artist, album);
        public static string GetTrackUrl(string artist, string track) => CatalogueUrlPrefix + GetTrackUrlPath(artist, track);
        
        public static string GetArtistId(string artist) => GetIdFromUrlPath(GetArtistUrlPath(artist));
        public static string GetAlbumId(string artist, string album) => GetIdFromUrlPath(GetAlbumUrlPath(artist, album));
        public static string GetTrackId(string artist, string track) => GetIdFromUrlPath(GetTrackUrlPath(artist, track));

        private static string GetIdFromUrlPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            using (var hasher = System.Security.Cryptography.SHA256.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(path);
                var hashBytes = hasher.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                    sb.Append(hashBytes[i].ToString("X2"));

                return sb.ToString();
            }
        }

        private static string GetArtistUrlPath(string artist) => HttpUtility.UrlEncode(artist);
        private static string GetAlbumUrlPath(string artist, string album) => $"{HttpUtility.UrlEncode(artist)}/{HttpUtility.UrlEncode(album)}";
        private static string GetTrackUrlPath(string artist, string track) => $"{HttpUtility.UrlEncode(artist)}/_/{HttpUtility.UrlEncode(track)}";

        private static Uri GetLargestImage(JToken imageSet)
        {
            var url = (string)(imageSet as JArray)?.LastOrDefault()?["#text"];
            return string.IsNullOrWhiteSpace(url) ? null : new Uri(url);
        }
    }
}
