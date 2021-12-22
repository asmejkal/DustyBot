using System;

namespace DustyBot.Service.Services.YouTube
{
    public class YouTubeVideoStatistics
    {
        public string Id { get; }
        public int Views { get; }
        public int Likes { get; }
        public DateTimeOffset PublishedAt { get; }

        public YouTubeVideoStatistics(string id, int views, int likes, DateTimeOffset publishedAt)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Views = views;
            Likes = likes;
            PublishedAt = publishedAt;
        }
    }
}
