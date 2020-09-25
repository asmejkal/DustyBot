using DustyBot.Database.Sql.StoredProcedures.Models;
using DustyBot.Database.Sql.UserDefinedTypes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public interface ILastFmStatsService : IDisposable
    {
        Task<IEnumerable<GetTopArtistsResult>> GetTopArtistsAsync(int count, CancellationToken ct);
        Task<IEnumerable<GetTopArtistsResult>> GetTopArtistsOfUsersAsync(IEnumerable<GetTopArtistsOfUsersTable> users, int count, CancellationToken ct);
        Task SetUserTracksAsync(IEnumerable<SetUserTracksTable> tracks, CancellationToken ct);
    }
}
