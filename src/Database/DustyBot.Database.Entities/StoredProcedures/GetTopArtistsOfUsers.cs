using DustyBot.Database.Core.StoredProcedures;
using DustyBot.Database.Entities.StoredProcedures.Models;
using DustyBot.Database.Entities.UserDefinedTypes;
using StoredProcedureEFCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Entities.StoredProcedures
{
    public static class GetTopArtistsOfUsersExtensions
    {
        public static Task<IReadOnlyList<GetTopArtistsResult>> GetTopArtistsOfUsersAsync(this DustyBotDbContext dbContext, IEnumerable<GetTopArtistsOfUsersTable> users, int count, CancellationToken ct)
        {
            return dbContext.LoadStoredProc("[DustyBot].[GetTopArtistsOfUsers]")
                .AddParam("@users", users, GetTopArtistsOfUsersTable.TypeName)
                .AddParam("@count", count)
                .ReadListResultAsync<GetTopArtistsResult>(ct);
        }
    }
}
