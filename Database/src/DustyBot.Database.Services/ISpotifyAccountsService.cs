using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.TableStorage.Tables;

namespace DustyBot.Database.Services
{
    public interface ISpotifyAccountsService
    {
        Task<SpotifyAccount?> GetUserAccountAsync(ulong userId, CancellationToken ct);
        Task AddOrUpdateUserAccountAsync(SpotifyAccount account, CancellationToken ct);
        Task RemoveUserAccountAsync(ulong id, CancellationToken ct);
    }
}
