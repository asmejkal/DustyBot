using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public interface INotificationSettingsService
    {
        Task<bool> GetIgnoreActiveChannelAsync(ulong userId, CancellationToken ct);
        Task<bool> ToggleIgnoreActiveChannelAsync(ulong userId, CancellationToken ct);
    }
}
