using DustyBot.Database.Entities.StoredProcedures.Models;
using DustyBot.Database.Entities.UserDefinedTypes;
using Safetica.Database.Core.StoredProcedures;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Entities.StoredProcedures
{
    public static class GetTopArtistsExtensions
    {
        public static Task<List<GetTopArtistsResult>> GetTopArtistsAsync(this DustyBotDbContext dbContext, int count, CancellationToken ct)
        {
            return dbContext.CreateStoredProcedure("[DustyBot].[GetTopArtists]")
                .AddParam("@count", count)
                .ExecuteReaderListAsync<GetTopArtistsResult>(ct);
        }
    }
}
