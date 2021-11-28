using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Utility;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public class NotificationSettingsService : INotificationSettingsService
    {
        private readonly IMongoDatabase _database;

        public NotificationSettingsService(IMongoDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public async Task BlockUserAsync(ulong userId, ulong targetUserId, CancellationToken ct)
        {
            await GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .With(b => b.AddToSet(x => x.BlockedUsers, targetUserId))
                .Upsert()
                .ExecuteAsync(ct);
        }

        public async Task<bool> GetIgnoreActiveChannelAsync(ulong userId, CancellationToken ct)
        {
            return await GetCollection()
                .Find(x => x.UserId == userId)
                .Project(x => x.IgnoreActiveChannel)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IEnumerable<ulong>> GetBlockedUsersAsync(ulong userId, CancellationToken ct)
        {
            return await GetCollection()
                .Find(x => x.UserId == userId)
                .Project(x => x.BlockedUsers)
                .FirstOrDefaultAsync(ct) ?? Enumerable.Empty<ulong>();
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

        public async Task UnblockUserAsync(ulong userId, ulong targetUserId, CancellationToken ct)
        {
            await GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .With(b => b.Pull(x => x.BlockedUsers, targetUserId))
                .Upsert()
                .ExecuteAsync(ct);
        }

        private IMongoCollection<UserNotificationSettings> GetCollection() =>
            _database.GetCollection<UserNotificationSettings>(typeof(UserNotificationSettings).Name);
    }
}
