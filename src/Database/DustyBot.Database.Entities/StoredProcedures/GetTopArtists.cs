using DustyBot.Database.Entities.StoredProcedures.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StoredProcedureEFCore;
using DustyBot.Database.Core.StoredProcedures;

namespace DustyBot.Database.Entities.StoredProcedures
{
    public static class GetTopArtistsExtensions
    {
        public static Task<IReadOnlyList<GetTopArtistsResult>> GetTopArtistsAsync(this DustyBotDbContext dbContext, int count, CancellationToken ct)
        {
            return dbContext.LoadStoredProc("[DustyBot].[GetTopArtists]")
                .AddParam("@count", count)
                .ReadListResultAsync<GetTopArtistsResult>(ct);
        }
    }
}
