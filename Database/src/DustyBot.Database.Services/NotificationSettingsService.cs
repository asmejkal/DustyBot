using System;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Utility;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public class NotificationSettingsService : INotificationSettingsService
    {
        private IMongoDatabase _db;

        public NotificationSettingsService(IMongoDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<bool> GetIgnoreActiveChannelAsync(ulong userId, CancellationToken ct)
        {
            return await GetCollection()
                .Find(x => x.UserId == userId)
                .Project(x => x.IgnoreActiveChannel)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<bool> ToggleIgnoreActiveChannelAsync(ulong userId, CancellationToken ct)
        {
            return await GetCollection()
                .FindOneAndUpdate(x => x.UserId == userId)
                .With(b => b.Toggle(x => x.IgnoreActiveChannel))
                .Upsert()
                .ReturnNew()
                .Project(x => x.IgnoreActiveChannel)
                .ExecuteAsync(ct);
        }

        private IMongoCollection<UserNotificationSettings> GetCollection() =>
            _db.GetCollection<UserNotificationSettings>(typeof(UserNotificationSettings).Name);
    }
}
