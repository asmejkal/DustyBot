using DustyBot.Database.Sql.StoredProcedures.Models;
using DustyBot.Database.Sql.UserDefinedTypes;
using DustyBot.Database.Sql.StoredProcedures.Utility;
using StoredProcedureEFCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Sql.StoredProcedures
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
