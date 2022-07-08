using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Database.Mongo.Collections.Reactions.Models;

namespace DustyBot.Service.Services.Reactions
{
    public interface IReactionsService
    {
        Task<int> AddReactionAsync(Snowflake guildId, string trigger, string response, CancellationToken ct);
        Task ClearReactionsAsync(Snowflake guildId, CancellationToken ct);
        Task<EditReactionResult> EditReactionAsync(Snowflake guildId, string idOrTrigger, string response, CancellationToken ct);
        Task<Stream?> ExportReactionsAsync(Snowflake guildId, CancellationToken ct);
        Task<ulong?> GetManagerRoleAsync(Snowflake guildId, CancellationToken ct);
        Task<IEnumerable<Reaction>> GetReactionsAsync(Snowflake guildId, CancellationToken ct);
        Task<IEnumerable<Reaction>> GetReactionsAsync(Snowflake guildId, string idOrTrigger, CancellationToken ct);
        Task<IEnumerable<ReactionStatistics>> GetReactionStatisticsAsync(Snowflake guildId, CancellationToken ct);
        Task<ReactionStatistics?> GetReactionStatisticsAsync(Snowflake guildId, string idOrTrigger, CancellationToken ct);
        Task<ImportReactionsResult> ImportReactionsAsync(Snowflake guildId, Uri reactionsFileUri, CancellationToken ct);
        Task<int> RemoveReactionsAsync(Snowflake guildId, string idOrTrigger, CancellationToken ct);
        Task<int> RenameReactionsAsync(Snowflake guildId, string idOrTrigger, string newTrigger, CancellationToken ct);
        Task ResetManagerRoleAsync(Snowflake guildId, CancellationToken ct);
        Task<IEnumerable<Reaction>> SearchReactionsAsync(Snowflake guildId, string searchInput, CancellationToken ct);
        Task<SetCooldownResult> SetCooldownAsync(Snowflake guildId, string idOrTrigger, TimeSpan cooldown, CancellationToken ct);
        Task SetManagerRoleAsync(Snowflake guildId, IRole role, CancellationToken ct);
    }

    public enum EditReactionResult
    {
        Success,
        NotFound,
        AmbiguousQuery
    }

    public enum ImportReactionsResult
    {
        Success,
        InvalidFile
    }

    public enum SetCooldownResult
    {
        Success,
        NotFound
    }
}