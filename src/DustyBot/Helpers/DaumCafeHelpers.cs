using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DustyBot.Framework.Utility;

namespace DustyBot.Helpers
{
    /// <summary>
    /// Daum API is dead, so we have to go the browser route...
    /// </summary>
    public static class DaumCafeHelpers
    {
        public static async Task<uint> GetLastPostId(string cafeId, string boardId) 
            => (await GetPostIds(cafeId, boardId).ConfigureAwait(false)).DefaultIfEmpty().Max();

        public static async Task<List<uint>> GetPostIds(string cafeId, string boardId)
        {
            var result = new List<uint>();
            var request = WebRequest.CreateHttp($"http://m.cafe.daum.net/{cafeId}/{boardId}");

            using (var response = await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync().ConfigureAwait(false);

                await Task.Run(() =>
                {
                    foreach (Match match in Regex.Matches(content, $"{cafeId}/{boardId}/([0-9]+)\""))
                    {
                        if (match.Groups.Count < 2)
                            continue;

                        uint id;
                        if (!uint.TryParse(match.Groups[1].Value, out id))
                            continue;

                        result.Add(id);
                    }
                }).ConfigureAwait(false);
            }

            return result;
        }

        public class PageMetadata
        {
            public string RelativeUrl { get; set; }
            public string Type { get; set; }
            public string Title { get; set; }
            public string ImageUrl { get; set; }
            public string Description { get; set; }
        }

        private static Regex _metaPropertyRegex = new Regex(@"<meta\s+property=""(.+)"".+content=""(.+)"".*>", RegexOptions.Compiled);

        public static async Task<PageMetadata> GetPageMetadata(string mobileUrl)
        {
            var request = WebRequest.CreateHttp(mobileUrl);
            
            using (var response = await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                var properties = new List<Tuple<string, string>>();

                await Task.Run(() =>
                {
                    var matches = _metaPropertyRegex.Matches(content);
                    foreach (Match match in matches)
                        properties.Add(Tuple.Create(match.Groups[1].Value, match.Groups[2].Value));

                }).ConfigureAwait(false);

                return new PageMetadata()
                {
                    RelativeUrl = properties.FirstOrDefault(x => x.Item1 == "og:url")?.Item2,
                    Type = properties.FirstOrDefault(x => x.Item1 == "og:type")?.Item2,
                    Title = properties.FirstOrDefault(x => x.Item1 == "og:title")?.Item2,
                    ImageUrl = properties.FirstOrDefault(x => x.Item1 == "og:image")?.Item2,
                    Description = WebUtility.HtmlDecode(properties.FirstOrDefault(x => x.Item1 == "og:description")?.Item2 ?? "")
                };
            }
        }
    }
}
