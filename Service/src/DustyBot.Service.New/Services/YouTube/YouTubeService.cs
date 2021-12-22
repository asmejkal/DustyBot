using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Core.Parsing;
using DustyBot.Database.Mongo.Collections.YouTube;
using DustyBot.Database.Mongo.Collections.YouTube.Models;
using DustyBot.Database.Services;
using DustyBot.Service.Services.YouTube;

namespace DustyBot.Service.Services.Log
{
    internal class YouTubeService : IYouTubeService
    {
        private readonly ISettingsService _settings;
        private readonly IYouTubeClient _youTubeClient;

        public YouTubeService(ISettingsService settings, IYouTubeClient youTubeClient)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _youTubeClient = youTubeClient ?? throw new ArgumentNullException(nameof(youTubeClient));
        }

        public async Task<IReadOnlyDictionary<YouTubeSong, AggregatedYouTubeVideoStatistics>> GetStatisticsAsync(
            Snowflake guildId,
            string? nameOrCategoryFilter = null,
            CancellationToken ct = default)
        {
            var settings = await _settings.Read<YouTubeSettings>(guildId, false, ct);
            if (settings == null)
                return new Dictionary<YouTubeSong, AggregatedYouTubeVideoStatistics>();

            var songs = settings.Songs;
            if (!string.IsNullOrEmpty(nameOrCategoryFilter))
            {
                songs = settings.Songs.Where(x => string.Compare(x.Category, nameOrCategoryFilter, true) == 0).ToList();
                if (!songs.Any())
                    songs = settings.Songs.Where(x => x.Name.Search(nameOrCategoryFilter, true)).ToList();
            }

            if (!songs.Any())
                return new Dictionary<YouTubeSong, AggregatedYouTubeVideoStatistics>();

            var statistics = await _youTubeClient.GetVideoStatisticsAsync(songs.SelectMany(x => x.VideoIds).Distinct(), ct);
            return songs.ToDictionary(x => x, x => new AggregatedYouTubeVideoStatistics(x.VideoIds.Select(x => statistics[x])));
        }

        public Task AddSongAsync(Snowflake guildId, string categoryName, string songName, IEnumerable<string> videoIds, CancellationToken ct)
        {
            return _settings.Modify(guildId, (YouTubeSettings s) => s.Songs.Add(new YouTubeSong(songName, videoIds, categoryName)));
        }

        public Task<bool> RemoveSongAsync(Snowflake guildId, string categoryName, string songName, CancellationToken ct)
        {
            return _settings.Modify(guildId, (YouTubeSettings s) =>
            {
                return s.Songs.RemoveAll(x => string.Compare(x.Name, songName, true) == 0 &&
                    string.Compare(x.Category, categoryName, true) == 0) > 0;
            }, ct);
        }

        public Task<bool> RenameSongAsync(Snowflake guildId, string oldName, string newName, CancellationToken ct)
        {
            return _settings.Modify(guildId, (YouTubeSettings s) =>
            {
                var songs = s.Songs.Where(x => string.Compare(x.Name, oldName, true) == 0);
                foreach (var song in songs)
                    song.Name = newName;

                return songs.Any();
            }, ct);
        }

        public Task<bool> MoveCategoryAsync(Snowflake guildId, string oldName, string newName, CancellationToken ct)
        {
            return _settings.Modify(guildId, (YouTubeSettings s) =>
            {
                var songs = s.Songs.Where(x => string.Compare(x.Category, oldName, true) == 0);
                foreach (var song in songs)
                    song.Category = newName;

                return songs.Any();
            }, ct);
        }

        public async Task<IEnumerable<YouTubeSong>> GetSongsAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<YouTubeSettings>(guildId, false, ct);
            return settings?.Songs ?? Enumerable.Empty<YouTubeSong>();
        }

        public async Task<IEnumerable<string>> GetCategoryRecommendationsAsync(Snowflake guildId, string excludedCategory, int limit, CancellationToken ct = default)
        {
            var settings = await _settings.Read<YouTubeSettings>(guildId, false, ct);
            return settings?.Songs
                .Select(x => x.Category)
                .Where(x => string.Compare(x, excludedCategory, true) != 0)
                .Where(x => string.Compare(x, YouTubeSong.DefaultCategory, true) != 0)
                .Distinct()
                .Take(limit)
                ?? Enumerable.Empty<string>();
        }
    }
}
