using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Core.Json;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace DustyBot.Service.Services.YouTube
{
    internal class YouTubeClient : IYouTubeClient
    {
        private const string ApiUrl = "https://www.googleapis.com/youtube/v3";
        private const int MaxVideoIdsPerRequest = 50;

        private readonly HttpClient _httpClient;
        private readonly YouTubeOptions _options;

        public YouTubeClient(HttpClient httpClient, IOptions<YouTubeOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IReadOnlyDictionary<string, YouTubeVideoStatistics>> GetVideoStatisticsAsync(IEnumerable<string> ids, CancellationToken ct)
        {
            var result = new Dictionary<string, YouTubeVideoStatistics>();
            foreach (var chunk in ids.Chunk(MaxVideoIdsPerRequest))
            {
                var requestUrl = $"{ApiUrl}/videos?part=statistics,snippet&id={string.Join(",", chunk)}&key={_options.ApiKey}";
                var response = await _httpClient.GetStringAsync(requestUrl, ct);

                var json = JObject.Parse(response);
                foreach (var item in json["items"] ?? Enumerable.Empty<JToken>())
                {
                    var statistics = item.RequiredValue("statistics");
                    var id = item.RequiredValue<string>("id");
                    result[id] = new YouTubeVideoStatistics(
                        id,
                        statistics.RequiredValue<int>("viewCount"),
                        statistics.RequiredValue<int>("likeCount"),
                        item.RequiredValue("snippet").RequiredValue<DateTime>("publishedAt"));
                }
            }
            
            return result;
        }
    }
}
