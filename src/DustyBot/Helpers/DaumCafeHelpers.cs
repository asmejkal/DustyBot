using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class DaumCafeHelpers
    {
        public static async Task<uint> GetLastPostId(string cafeId, string boardId)
        {
            var request = WebRequest.CreateHttp($"http://m.cafe.daum.net/{cafeId}/{boardId}");

            uint highestId = 0;

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

                        if (id > highestId)
                            highestId = id;
                    }
                }).ConfigureAwait(false);
            }

            return highestId;
        }
    }
}
