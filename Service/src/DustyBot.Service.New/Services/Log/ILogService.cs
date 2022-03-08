using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disqord;

namespace DustyBot.Service.Services.Log
{
    public interface ILogService
    {
        Task AddChannelFilterAsync(Snowflake guildId, IEnumerable<IMessageGuildChannel> channels, CancellationToken ct);

        Task AddPrefixFilterAsync(Snowflake guildId, string prefix, CancellationToken ct);

        Task DisableMessageLoggingAsync(Snowflake guildId, CancellationToken ct);

        Task EnableMessageLoggingAsync(Snowflake guildId, IMessageGuildChannel channel, CancellationToken ct);

        Task<IEnumerable<ulong>> GetChannelFiltersAsync(Snowflake guildId, CancellationToken ct);

        Task<IEnumerable<string>> GetPrefixFiltersAsync(Snowflake guildId, CancellationToken ct);

        Task RemoveChannelFilterAsync(Snowflake guildId, IEnumerable<IMessageGuildChannel> channels, CancellationToken ct);

        Task<bool> RemovePrefixFilterAsync(Snowflake guildId, string prefix, CancellationToken ct);

        Task ClearPrefixFiltersAsync(Snowflake guildId, CancellationToken ct);
    }
}