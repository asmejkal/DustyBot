using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Mongo.Utility;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public class DaumCafeSettingsService : IDaumCafeSettingsService
    {
        private readonly IMongoDatabase _database;

        public DaumCafeSettingsService(IMongoDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public async Task<UserDaumCafeSettings?> ReadAsync(ulong userId, CancellationToken ct = default)
        {
            return await GetCollection().Find(x => x.UserId == userId).FirstOrDefaultAsync(ct);
        }

        public async Task<DaumCafeCredential?> GetCredentialAsync(ulong userId, Guid credentialId, CancellationToken ct = default)
        {
            var credentials = await ReadAsync(userId, ct);
            return credentials?.Credentials.SingleOrDefault(x => x.Id == credentialId);
        }

        public Task AddCredentialAsync(ulong userId, DaumCafeCredential credential, CancellationToken ct = default)
        {
            return GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .Upsert()
                .With(b => b.Push(x => x.Credentials, credential))
                .ExecuteAsync(ct);
        }

        public async Task<bool> RemoveCredentialAsync(ulong userId, Guid credentialId, CancellationToken ct = default)
        {
            var result = await GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .With(b => b.PullFilter(x => x.Credentials, x => x.Id == credentialId))
                .ExecuteAsync(ct);

            return result.ModifiedCount > 0;
        }

        public Task ResetAsync(ulong userId, CancellationToken ct = default)
        {
            return GetCollection().DeleteOneAsync(x => x.UserId == userId, ct);
        }

        private IMongoCollection<UserDaumCafeSettings> GetCollection() =>
            _database.GetCollection<UserDaumCafeSettings>(typeof(UserDaumCafeSettings).Name);
    }
}
