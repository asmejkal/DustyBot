using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public enum LfStatsPeriod
    {
        Overall,
        Day,
        Week,
        Month,
        QuarterYear,
        HalfYear,
        Year
    }

    public interface ILfEntity
    {
        object Id { get; }
        int Playcount { get; }
    }

    public class LfArtist : ILfEntity
    {
        public object Id => Name;
        public string Name { get; }
        public string Url { get; }
        public int Playcount { get; }

        internal LfArtist(string name, string url, int playcount = -1)
        {
            Name = name;
            Url = url;
            Playcount = playcount;
        }
    }

    public class LfArtistDetail : LfArtist
    {
        public Uri Image { get; }

        public IList<LfAlbum> TopAlbums { get; }
        public IList<LfTrack> TopTracks { get; }

        public int AlbumsListened { get; }
        public int TracksListened { get; }

        internal LfArtistDetail(string name, string url, Uri image, IEnumerable<LfAlbum> topAlbums, IEnumerable<LfTrack> topTracks, int albumsListened, int tracksListened, int playcount = -1)
            : base(name, url, playcount)
        {
            Image = image;
            TopAlbums = topAlbums.ToList();
            TopTracks = topTracks.ToList();
            AlbumsListened = albumsListened;
            TracksListened = tracksListened;
        }
    }

    public class LfAlbum : ILfEntity
    {
        public object Id => (Artist.Name, Name);
        public string Name { get; }
        public LfArtist Artist { get; }
        public string Url { get; }
        public int Playcount { get; }

        internal LfAlbum(string name, LfArtist artist, string url, int playcount = -1)
        {
            Name = name;
            Artist = artist;
            Url = url;
            Playcount = playcount;
        }
    }

    public class LfTrack : ILfEntity
    {
        public object Id => (Artist.Name, Name);
        public string Name { get; }
        public LfArtist Artist => Album.Artist;
        public LfAlbum Album { get; }
        public string Url { get; }
        public int Playcount { get; }

        internal LfTrack(string name, LfAlbum album, string url, int playcount = -1)
        {
            Name = name;
            Album = album;
            Url = url;
            Playcount = playcount;
        }
    }

    public class LfScore<T>
        where T : ILfEntity
    {
        public T Entity { get; }
        public object Id => Entity.Id;
        public int Playcount => Entity.Playcount;
        public double Score { get; }

        internal LfScore(T entity, double score)
        {
            Entity = entity;
            Score = score;
        }
    }

    public class LastFmClient
    {
        public const int MaxRecentTracksPageSize = 200;

        private static readonly IReadOnlyDictionary<LfStatsPeriod, string> StatsPeriodMapping = new Dictionary<LfStatsPeriod, string>()
        {
            { LfStatsPeriod.Overall, "overall" },
            { LfStatsPeriod.Week, "7day" },
            { LfStatsPeriod.Month, "1month" },
            { LfStatsPeriod.QuarterYear, "3month" },
            { LfStatsPeriod.HalfYear, "6month" },
            { LfStatsPeriod.Year, "12month" },
        };

        private static readonly IReadOnlyDictionary<LfStatsPeriod, string> StatsPeriodWebMapping = new Dictionary<LfStatsPeriod, string>()
        {
            { LfStatsPeriod.Overall, "ALL" },
            { LfStatsPeriod.Week, "LAST_7_DAYS" },
            { LfStatsPeriod.Month, "LAST_30_DAYS" },
            { LfStatsPeriod.QuarterYear, "LAST_90_DAYS" },
            { LfStatsPeriod.HalfYear, "LAST_180_DAYS" },
            { LfStatsPeriod.Year, "LAST_365_DAYS" },
        };

        // Same values as on Last.fm's website
        private static readonly IReadOnlyDictionary<LfStatsPeriod, TimeSpan> StatsPeriodTimeMapping = new Dictionary<LfStatsPeriod, TimeSpan>()
        {
            { LfStatsPeriod.Day, TimeSpan.FromDays(1) },
            { LfStatsPeriod.Week, TimeSpan.FromDays(7) },
            { LfStatsPeriod.Month, TimeSpan.FromDays(30) },
            { LfStatsPeriod.QuarterYear, TimeSpan.FromDays(90) },
            { LfStatsPeriod.HalfYear, TimeSpan.FromDays(180) },
            { LfStatsPeriod.Year, TimeSpan.FromDays(365) },
        };

        public string User { get; }
        private string Key { get; }

        public LastFmClient(string user, string key)
        {
            User = user;
            Key = key;
        }

        public async Task<dynamic> GetUserInfo()
        {
            var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=user.getinfo&user={User}&api_key={Key}&format=json");
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);

                return root?.user;
            }
        }

        public Task<(IEnumerable<dynamic> tracks, bool nowPlaying)> GetRecentTracks(LfStatsPeriod period, int count = int.MaxValue)
            => period == LfStatsPeriod.Overall ? GetRecentTracks(count: count) : GetRecentTracks(StatsPeriodTimeMapping[period], count);

        public Task<(IEnumerable<dynamic> tracks, bool nowPlaying)> GetRecentTracks(TimeSpan period, int count = int.MaxValue)
            => GetRecentTracks(DateTimeOffset.UtcNow - period, count);

        public async Task<(IEnumerable<dynamic> tracks, bool nowPlaying)> GetRecentTracks(DateTimeOffset? from = null, int count = int.MaxValue)
        {
            var retrieved = 0;
            var page = 1;
            var results = Enumerable.Empty<dynamic>();
            var nowPlaying = false;
            while (retrieved < count)
            {
                var result = await GetRecentTracksPage(page, Math.Min(count, MaxRecentTracksPageSize), from);
                nowPlaying = page == 1 && result.nowPlaying;
                results = results.Concat(result.tracks);

                retrieved += Math.Min(count, MaxRecentTracksPageSize);
                if (!result.remaining)
                    break;

                page++;
            }

            return (results, nowPlaying);
        }

        public async Task<(IEnumerable<dynamic> tracks, bool nowPlaying, bool remaining)> GetRecentTracksPage(int page, int count, DateTimeOffset? from = null)
        {
            var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=user.getrecenttracks&user={User}&api_key={Key}&format=json&limit={count}&page={page}&extended=1" + (from.HasValue ? $"&from={from.Value.ToUnixTimeSeconds()}" : string.Empty));
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                var tracks = root?.recenttracks?.track as JArray;

                if (tracks != null && tracks.Any())
                {
                    var nowPlaying = page == 1 && string.Compare((string)tracks[0]["@attr"]?["nowplaying"], "true", true) == 0;
                    var more = page < (int)root.recenttracks["@attr"].totalPages;
                    return (((IEnumerable<dynamic>)tracks).ToList(), nowPlaying, more);
                }
                else
                    return (new List<dynamic>(), false, false);
            }
        }

        public async Task<dynamic> GetArtistInfo(string artist)
        {
            var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=artist.getInfo&api_key={Key}&artist={Uri.EscapeDataString(artist)}&format=json&username={User}");
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                return root.artist;
            }
        }

        public async Task<dynamic> GetTrackInfo(string artist, string track)
        {
            var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=track.getInfo&api_key={Key}&artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}&format=json&username={User}");
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                return root.track;
            }
        }

        private class RecentTopItem
        {
            public int Playcount { get; set; }
            public dynamic Entity { get; set; }
            public dynamic Track { get; set; }
        }

        private async Task<IEnumerable<RecentTopItem>> GetTopFromRecent(
            Func<dynamic, dynamic> entitySelector, 
            Func<dynamic, object> idSelector,
            TimeSpan? period = null,
            int count = int.MaxValue)
        {
            var (tracks, nowPlaying) = period.HasValue ? await GetRecentTracks(period.Value, count) : await GetRecentTracks(count: count);
            if (nowPlaying)
                tracks = tracks.Skip(1);

            var stats = new Dictionary<object, RecentTopItem>();
            foreach (var track in tracks)
            {
                var entity = entitySelector(track);
                var id = (object)idSelector(track);
                if (id == null)
                    continue;

                if (stats.TryGetValue(id, out RecentTopItem value))
                    value.Playcount++;
                else
                    stats[id] = new RecentTopItem() { Playcount = 1, Entity = entity, Track = track };
            }

            return stats.Values.OrderByDescending(x => x.Playcount);
        }

        public async Task<IEnumerable<LfArtist>> GetTopArtists(LfStatsPeriod period, int count)
        {
            if (period == LfStatsPeriod.Day)
            {
                var results = await GetTopFromRecent(x => x.artist, x => (string)x.artist.name, StatsPeriodTimeMapping[period]);
                return results.Take(count).Select(x => new LfArtist((string)x.Entity.name, (string)x.Entity.url, x.Playcount));
            }
            else
            {
                var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=user.gettopartists&user={User}&api_key={Key}&period={StatsPeriodMapping[period]}&limit={count}&format=json");
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    dynamic root = JObject.Parse(text);
                    var results = root?.topartists?.artist as JArray ?? Enumerable.Empty<dynamic>();
                    return results.Select(x => new LfArtist((string)x.name, (string)x.url, (int)x.playcount));
                }
            }
        }

        public async Task<IEnumerable<LfAlbum>> GetTopAlbums(LfStatsPeriod period, int count)
        {
            if (period == LfStatsPeriod.Day)
            {
                var results = await GetTopFromRecent(x => x.album, x => ((string)x.artist.name, (string)x.album["#text"]), StatsPeriodTimeMapping[period]);
                return results.Take(count).Select(x => new LfAlbum((string)x.Entity["#text"], new LfArtist((string)x.Track.artist.name, (string)x.Track.artist.url), null, (int)x.Playcount));
            }
            else
            {
                var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=user.gettopalbums&user={User}&api_key={Key}&period={StatsPeriodMapping[period]}&limit={count}&format=json");
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    dynamic root = JObject.Parse(text);
                    var results = root?.topalbums?.album as JArray ?? Enumerable.Empty<dynamic>();
                    return results.Select(x => new LfAlbum((string)x.name, new LfArtist((string)x.artist.name, (string)x.artist.url), (string)x.url, (int)x.playcount));
                }
            }
        }

        public async Task<IEnumerable<LfTrack>> GetTopTracks(LfStatsPeriod period, int count)
        {
            if (period == LfStatsPeriod.Day)
            {
                var results = await GetTopFromRecent(x => x, x => ((string)x.artist.name, (string)x.name), StatsPeriodTimeMapping[period]);
                return results.Take(count).Select(x => new LfTrack((string)x.Entity.name, new LfAlbum((string)x.Track.album.name, new LfArtist((string)x.Track.artist.name, (string)x.Track.artist.url), null), (string)x.Entity.url, (int)x.Playcount));
            }
            else
            {
                var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=user.gettoptracks&user={User}&api_key={Key}&period={StatsPeriodMapping[period]}&limit={count}&format=json");
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var text = await reader.ReadToEndAsync();
                    dynamic root = JObject.Parse(text);
                    var results = root?.toptracks?.track as JArray ?? Enumerable.Empty<dynamic>();
                    return results.Select(x => new LfTrack((string)x.name, new LfAlbum(null, new LfArtist((string)x.artist.name, (string)x.artist.url), null), (string)x.url, (int)x.playcount));
                }
            }
        }

        public Task<int> GetTotalPlaycount(LfStatsPeriod period) 
            => period == LfStatsPeriod.Overall ? GetTotalPlaycount() : GetTotalPlaycount(StatsPeriodTimeMapping[period]);

        public Task<int> GetTotalPlaycount(TimeSpan period)
            => GetTotalPlaycount(DateTimeOffset.UtcNow - period);

        public async Task<int> GetTotalPlaycount(DateTimeOffset? from = null)
        {
            var request = WebRequest.CreateHttp($"http://ws.audioscrobbler.com/2.0/?method=user.getrecenttracks&user={User}&api_key={Key}&limit=1&format=json" + (from.HasValue ? $"&from={from.Value.ToUnixTimeSeconds()}" : string.Empty));
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                return (int)root.recenttracks["@attr"].total;
            }
        }

        public async Task<LfArtistDetail> GetArtistDetail(string artist, LfStatsPeriod period)
        {
            try
            {
                var infoTask = GetArtistInfo(artist);
                var request = WebRequest.CreateHttp($"https://www.last.fm/user/{User}/library/music/{Uri.EscapeDataString(artist)}?date_preset={StatsPeriodWebMapping[period]}");
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var content = await reader.ReadToEndAsync();

                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    // Get artist info
                    var info = await infoTask;
                    var name = (string)info.name;
                    var url = (string)info.url;

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
                        var iii = topList.Descendants("tr").Where(x => x.HasClass("chartlist-row") || x.HasClass("chartlist-row\n"));
                        foreach (var item in iii)
                        {
                            var itemNameLink = item.Descendants("td").First(x => x.HasClass("chartlist-name")).Descendants("a").First();
                            var itemUrl = "https://www.last.fm" + itemNameLink.GetAttributeValue("href", null);

                            var scrobbleText = item.Descendants("span").First(x => x.HasClass("chartlist-count-bar-value")).InnerText;
                            var scrobbleCount = int.Parse(Regex.Match(scrobbleText, @"[\d,.\s]+").Value, System.Globalization.NumberStyles.Any);
                            yield return (WebUtility.HtmlDecode(itemNameLink.InnerText), itemUrl, scrobbleCount);
                        }
                    }

                    var topLists = doc.DocumentNode.Descendants("table").Where(x => x.HasClass("chartlist") || x.HasClass("chartlist\n")).ToList();
                    var topAlbums = Enumerable.Empty<LfAlbum>();
                    if (topLists.Any())
                        topAlbums = GetTopListItems(topLists.First()).Select(x => new LfAlbum(x.name, new LfArtist(name, url), x.url, x.playcount));

                    var topTracks = Enumerable.Empty<LfTrack>();
                    if (topLists.Skip(1).Any())
                        topTracks = GetTopListItems(topLists.Skip(1).First()).Select(x => new LfTrack(x.name, null, x.url, x.playcount));

                    var imageUri = string.IsNullOrEmpty(image) ? null : new Uri(image);
                    return new LfArtistDetail(name, url, imageUri, topAlbums, topTracks, albumsListened, tracksListened, playcount);
                }
            }
            catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }    
        }

        public async Task<IEnumerable<LfScore<LfArtist>>> GetArtistScores(LfStatsPeriod period, int count, Task<int> totalPlaycount)
        {
            var playcount = await totalPlaycount;
            return (await GetTopArtists(period, count)).Select(x => new LfScore<LfArtist>(x, (double)x.Playcount / playcount));
        }

        public async Task<IEnumerable<LfScore<LfAlbum>>> GetAlbumScores(LfStatsPeriod period, int count, Task<int> totalPlaycount)
        {
            var playcount = await totalPlaycount;
            return (await GetTopAlbums(period, count)).Select(x => new LfScore<LfAlbum>(x, (double)x.Playcount / playcount));
        }

        public async Task<IEnumerable<LfScore<LfTrack>>> GetTrackScores(LfStatsPeriod period, int count, Task<int> totalPlaycount)
        {
            var playcount = await totalPlaycount;
            return (await GetTopTracks(period, count)).Select(x => new LfScore<LfTrack>(x, (double)x.Playcount / playcount));
        }

        public static string GetLargestImage(dynamic imageSet) 
            => (string)(imageSet as JArray)?.LastOrDefault()?["#text"];
    }
}
