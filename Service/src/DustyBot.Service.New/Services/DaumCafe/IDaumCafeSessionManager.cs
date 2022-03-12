using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections.DaumCafe.Models;
using DustyBot.DaumCafe;

namespace DustyBot.Service.Services.DaumCafe
{
    public interface IDaumCafeSessionManager
    {
        Task<DaumCafeSession> GetSessionAsync(DaumCafeFeed feed, CancellationToken ct);
    }
}