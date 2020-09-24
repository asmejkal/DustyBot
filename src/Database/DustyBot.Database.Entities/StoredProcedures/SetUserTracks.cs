using DustyBot.Database.Core.StoredProcedures;
using DustyBot.Database.Entities.UserDefinedTypes;
using StoredProcedureEFCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Entities.StoredProcedures
{
    public static class SetUserTracksExtensions
    {
        public static Task SetUserTracksAsync(this DustyBotDbContext dbContext, IEnumerable<SetUserTracksTable> tracks, CancellationToken ct)
        {
            return dbContext.LoadStoredProc("[DustyBot].[SetUserTracks]")
                .AddParam("@tracks", tracks, SetUserTracksTable.TypeName)
                .ExecNonQueryAsync(ct);
        }
    }
}
