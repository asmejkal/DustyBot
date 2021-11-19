using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public interface INotificationSettingsService
    {
        Task<bool> GetIgnoreActiveChannelAsync(ulong userId, CancellationToken ct);
        Task<bool> ToggleIgnoreActiveChannelAsync(ulong userId, CancellationToken ct);
        Task BlockUserAsync(ulong userId, ulong targetUserId, CancellationToken ct);
        Task UnblockUserAsync(ulong userId, ulong targetUserId, CancellationToken ct);
        Task<IEnumerable<ulong>> GetBlockedUsersAsync(ulong userId, CancellationToken ct);
    }
}
