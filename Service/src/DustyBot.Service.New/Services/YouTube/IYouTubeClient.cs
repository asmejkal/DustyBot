using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Service.Services.YouTube
{
    internal interface IYouTubeClient
    {
        Task<IReadOnlyDictionary<string, YouTubeVideoStatistics>> GetVideoStatisticsAsync(IEnumerable<string> ids, CancellationToken ct);
    }
}