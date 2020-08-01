using DustyBot.Database.Entities.UserDefinedTypes;
using Safetica.Database.Core.StoredProcedures;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Entities.StoredProcedures
{
    public static class SetUserTracksExtensions
    {
        public static Task SetUserTracksAsync(this DustyBotDbContext dbContext, IEnumerable<SetUserTracksTable> tracks, CancellationToken ct)
        {
            return dbContext.CreateStoredProcedure("[DustyBot].[SetUserTracks]")
                .AddParam("@tracks", tracks, SetUserTracksTable.TypeName)
                .ExecuteNonQueryAsync(ct);
        }
    }
}
