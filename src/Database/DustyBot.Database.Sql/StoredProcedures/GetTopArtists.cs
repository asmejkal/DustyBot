﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StoredProcedureEFCore;
using DustyBot.Database.Sql.StoredProcedures.Utility;
using DustyBot.Database.Sql.StoredProcedures.Models;

namespace DustyBot.Database.Sql.StoredProcedures
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
