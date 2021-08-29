using System;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Mongo.Utility;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public class CredentialsService : ICredentialsService
    {
        private IMongoDatabase _db;

        public CredentialsService(IMongoDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Task<UserCredentials> ReadAsync(ulong userId, CancellationToken ct = default)
        {
            return GetCollection().Find(x => x.UserId == userId).FirstOrDefaultAsync();
        }

        public Task AddAsync(ulong userId, Credentials credentials, CancellationToken ct = default)
        {
            return GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .Upsert()
                .With(b => b.Push(x => x.Credentials, credentials))
                .ExecuteAsync(ct);
        }

        public async Task<bool> RemoveAsync(ulong userId, Guid credentialsId, CancellationToken ct = default)
        {
            var result = await GetCollection()
                .UpdateOne(x => x.UserId == userId)
                .With(b => b.PullFilter(x => x.Credentials, x => x.Id == credentialsId))
                .ExecuteAsync(ct);

            return result.ModifiedCount > 0;
        }

        public Task ResetAsync(ulong userId, CancellationToken ct = default)
        {
            return GetCollection().DeleteOneAsync(x => x.UserId == userId, ct);
        }

        private IMongoCollection<UserCredentials> GetCollection() =>
            _db.GetCollection<UserCredentials>(typeof(UserCredentials).Name);
    }
}
