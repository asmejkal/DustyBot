using DustyBot.Database.Entities.StoredProcedures.Models;
using DustyBot.Database.Entities.UserDefinedTypes;
using Safetica.Database.Core.StoredProcedures;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Entities.StoredProcedures
{
    public static class GetTopArtistsOfUsersExtensions
    {
        public static Task<List<GetTopArtistsResult>> GetTopArtistsOfUsersAsync(this DustyBotDbContext dbContext, IEnumerable<GetTopArtistsOfUsersTable> users, int count, CancellationToken ct)
        {
            return dbContext.CreateStoredProcedure("[DustyBot].[GetTopArtistsOfUsers]")
                .AddParam("@users", users, GetTopArtistsOfUsersTable.TypeName)
                .AddParam("@count", count)
                .ExecuteReaderListAsync<GetTopArtistsResult>(ct);
        }
    }
}
