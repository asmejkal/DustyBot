using System;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Utility;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public class LastFmSettingsService : ILastFmSettingsService
    {
        private IMongoDatabase _db;

        public LastFmSettingsService(IMongoDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Task<LastFmUserSettings> ReadAsync(ulong userId, CancellationToken ct = default)
        {
            return GetCollection().Find(x => x.UserId == userId).FirstOrDefaultAsync(ct);
        }

        public Task SetUsernameAsync(ulong userId, string username, bool anonymous, CancellationToken ct = default)
        {
            return GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .Upsert()
                .With(b => b.Set(x => x.LastFmUsername, username).Set(x => x.Anonymous, anonymous))
                .ExecuteAsync(ct);
        }

        public Task ResetAsync(ulong userId, CancellationToken ct = default)
        {
            return GetCollection().DeleteOneAsync(x => x.UserId == userId, ct);
        }

        private IMongoCollection<LastFmUserSettings> GetCollection() =>
            _db.GetCollection<LastFmUserSettings>(typeof(LastFmUserSettings).Name);
    }
}
