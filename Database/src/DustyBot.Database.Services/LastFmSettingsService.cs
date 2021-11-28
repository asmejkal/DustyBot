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
        private readonly IMongoDatabase _database;

        public LastFmSettingsService(IMongoDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
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
            _database.GetCollection<LastFmUserSettings>(typeof(LastFmUserSettings).Name);
    }
}
