using DustyBot.Database.Entities;
using DustyBot.Database.Entities.StoredProcedures;
using DustyBot.Database.Entities.StoredProcedures.Models;
using DustyBot.Database.Entities.UserDefinedTypes;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public class LastFmService : BaseSqlService, ILastFmService
    {
        public LastFmService(DustyBotDbContext dbContext)
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
