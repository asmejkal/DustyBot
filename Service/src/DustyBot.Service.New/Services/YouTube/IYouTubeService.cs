using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Database.Mongo.Collections.YouTube.Models;
using DustyBot.Service.Services.YouTube;

namespace DustyBot.Service.Services.Log
{
    public interface IYouTubeService
    {
        Task AddSongAsync(Snowflake guildId, string categoryName, string songName, IEnumerable<string> videoIds, CancellationToken ct);
        Task<IEnumerable<YouTubeSong>> GetSongsAsync(Snowflake guildId, CancellationToken ct);
        Task<IReadOnlyDictionary<YouTubeSong, AggregatedYouTubeVideoStatistics?>> GetStatisticsAsync(Snowflake guildId, string? nameOrCategoryFilter = null, CancellationToken ct = default);
        Task<bool> MoveCategoryAsync(Snowflake guildId, string oldName, string newName, CancellationToken ct);
        Task<bool> RemoveSongAsync(Snowflake guildId, string categoryName, string songName, CancellationToken ct);
        Task<int> ClearSongsAsync(Snowflake guildId, string categoryName, CancellationToken ct);
        Task<bool> RenameSongAsync(Snowflake guildId, string oldName, string newName, CancellationToken ct);
        Task<IEnumerable<string>> GetCategoryRecommendationsAsync(Snowflake guildId, string excludedCategory, int limit, CancellationToken ct = default);
    }
}