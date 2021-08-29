using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;

namespace DustyBot.Database.Services
{
    public interface ILastFmSettingsService
    {
        Task<LastFmUserSettings> ReadAsync(ulong userId, CancellationToken ct = default);
        Task ResetAsync(ulong userId, CancellationToken ct = default);
        Task SetUsernameAsync(ulong userId, string username, bool anonymous, CancellationToken ct = default);
    }
}