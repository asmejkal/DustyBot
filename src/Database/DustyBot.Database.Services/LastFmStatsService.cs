using DustyBot.Database.Sql;
using DustyBot.Database.Sql.StoredProcedures;
using DustyBot.Database.Sql.StoredProcedures.Models;
using DustyBot.Database.Sql.UserDefinedTypes;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public class LastFmStatsService : BaseSqlService, ILastFmStatsService
    {
        public LastFmStatsService(DustyBotDbContext dbContext)
            : base(dbContext)
        {
        }

        public async Task<IEnumerable<GetTopArtistsResult>> GetTopArtistsAsync(int count, CancellationToken ct)
        {
            return await DbContext.GetTopArtistsAsync(count, ct);
        }

        public async Task<IEnumerable<GetTopArtistsResult>> GetTopArtistsOfUsersAsync(IEnumerable<GetTopArtistsOfUsersTable> users, int count, CancellationToken ct)
        {
            return await DbContext.GetTopArtistsOfUsersAsync(users, count, ct);
        }

        public Task SetUserTracksAsync(IEnumerable<SetUserTracksTable> tracks, CancellationToken ct)
        {
            return DbContext.SetUserTracksAsync(tracks, ct);
        }
    }
}
