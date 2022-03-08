using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Database.Mongo.Collections.Notifications.Models;

namespace DustyBot.Service.Services.Notifications
{
    public interface INotificationsService
    {
        Task<AddKeywordsResult> AddKeywordsAsync(Snowflake guildId, Snowflake userId, IEnumerable<string> keywords, CancellationToken ct);
        Task BlockUserAsync(Snowflake userId, Snowflake targetUserId, CancellationToken ct);
        Task ClearKeywordsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct);
        Task<IEnumerable<Notification>> GetKeywordsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct);
        Task PauseNotificationsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct);
        Task<RemoveKeywordResult> RemoveKeywordAsync(Snowflake guildId, Snowflake userId, string keyword, CancellationToken ct);
        Task ResumeNotificationsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct);
        Task<bool> ToggleActivityDetectionAsync(Snowflake userId, CancellationToken ct);
        Task<bool> ToggleOptOutAsync(Snowflake userId, CancellationToken ct);
        Task<bool> ToggleIgnoredChannelAsync(Snowflake guildId, Snowflake userId, Snowflake channelId, CancellationToken ct);
        Task UnblockUserAsync(Snowflake userId, Snowflake targetUserId, CancellationToken ct);
    }

    public enum AddKeywordsResult
    {
        Success,
        KeywordTooShort,
        KeywordTooLong,
        DuplicateKeyword,
        TooManyKeywords
    }

    public enum RemoveKeywordResult
    {
        Success,
        NotFound
    }
}