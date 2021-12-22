using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Service.Services.YouTube
{
    public class AggregatedYouTubeVideoStatistics
    {
        public IReadOnlyCollection<YouTubeVideoStatistics> Statistics { get; }
        
        public int Views { get; }
        public int Likes { get; }
        public DateTimeOffset FirstPublishedAt { get; }

        public AggregatedYouTubeVideoStatistics(IEnumerable<YouTubeVideoStatistics> statistics)
        {
            Statistics = statistics?.ToList() ?? throw new ArgumentNullException(nameof(statistics));

            Views = statistics.Sum(x => x.Views);
            Likes = statistics.Sum(x => x.Likes);
            FirstPublishedAt = statistics.MinBy(x => x.PublishedAt)?.PublishedAt 
                ?? throw new ArgumentException("Aggregate must have at least one item", nameof(statistics));
        }
    }
}
