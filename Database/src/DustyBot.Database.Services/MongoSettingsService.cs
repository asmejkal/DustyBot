using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DustyBot.Core.Async;
using DustyBot.Database.Core.Settings;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Serializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public sealed class MongoSettingsService : ISettingsService, IDisposable
    {
        private readonly KeyedSemaphoreSlim<(Type Type, ulong GuildId)> _serverSettingsMutex = new KeyedSemaphoreSlim<(Type Type, ulong GuildId)>(1, maxPoolSize: int.MaxValue);
        private readonly KeyedSemaphoreSlim<Type> _globalSettingsMutex = new KeyedSemaphoreSlim<Type>(1, maxPoolSize: int.MaxValue);

        private readonly IMongoDatabase _database;

        static MongoSettingsService()
        {
            BsonSerializer.RegisterSerializer(DateTimeSerializer.LocalInstance);
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new SecureStringSerializer());
        }

        public MongoSettingsService(IMongoDatabase database)
        {
            _database = database;
        }

        public Task<T> Read<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings, new()
        {
            return SafeGetDocument(unchecked((long)serverId), _serverSettingsMutex, (typeof(T), serverId), () => Create<T>(serverId), createIfNeeded);
        }

        public async Task<IEnumerable<T>> Read<T>()
            where T : IServerSettings
        {
            var cursor = await _database.GetCollection<T>(typeof(T).Name).Find(Builders<T>.Filter.Empty).ToCursorAsync();
            return cursor.ToEnumerable();
        }

        public async Task Modify<T>(ulong serverId, Action<T> action)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                action(settings);
                await UpsertSettings(serverId, settings);
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, U> action)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                var result = action(settings);
                await UpsertSettings(serverId, settings);

                return result;
            }
        }

        public async Task Modify<T>(ulong serverId, Func<T, Task> action)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                await action(settings);
                await UpsertSettings(serverId, settings);
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                var result = await action(settings);
                await UpsertSettings(serverId, settings);

                return result;
            }
        }

        public async Task<T> ReadGlobal<T>()
            where T : new()
        {
            return await SafeGetDocument<T, Type>(unchecked((long)CollectionConstants.GlobalSettingId), _globalSettingsMutex, typeof(T));
        }

        public async Task ModifyGlobal<T>(Action<T> action)
            where T : new()
        {
            using (await _globalSettingsMutex.ClaimAsync(typeof(T)))
            {
                var settings = await GetDocument<T>(unchecked((long)CollectionConstants.GlobalSettingId));
                action(settings);
                await UpsertSettings(unchecked((long)CollectionConstants.GlobalSettingId), settings);
            }
        }

        public async Task<U> ModifyGlobal<T, U>(Func<T, U> action)
            where T : new()
        {
            using (await _globalSettingsMutex.ClaimAsync(typeof(T)))
            {
                var settings = await GetDocument<T>(unchecked((long)CollectionConstants.GlobalSettingId));
                var result = action(settings);
                await UpsertSettings(unchecked((long)CollectionConstants.GlobalSettingId), settings);

                return result;
            }
        }

        public void Dispose()
        {
            _serverSettingsMutex?.Dispose();
            _globalSettingsMutex?.Dispose();
        }

        private async Task<T> GetDocument<T>(long id, Func<Task<T>> creator, bool createIfNeeded = true)
        {
            var collection = _database.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
            if (settings == null && createIfNeeded)
            {
                // Create
                settings = await creator();
                await collection.InsertOneAsync(settings);
            }

            return settings;
        }

        private async Task<T> GetDocument<T>(long id, bool createIfNeeded = true)
            where T : new()
        {
            return await GetDocument(id, () => Task.FromResult(new T()), createIfNeeded);
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(long id, KeyedSemaphoreSlim<TMutexKey> mutex, TMutexKey mutexKey, Func<Task<T>> creator, bool createIfNeeded = true)
        {
            var collection = _database.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
            if (settings == null && createIfNeeded)
            {
                // Create
                using (await mutex.ClaimAsync(mutexKey))
                {
                    collection = _database.GetCollection<T>(typeof(T).Name);
                    settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
                    if (settings == null)
                    {
                        settings = await creator();
                        await collection.InsertOneAsync(settings);
                    }
                }
            }

            return settings;
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(long id, KeyedSemaphoreSlim<TMutexKey> mutex, TMutexKey mutexKey, bool createIfNeeded = true)
            where T : new()
        {
            return await SafeGetDocument(id, mutex, mutexKey, () => Task.FromResult(new T()), createIfNeeded);
        }

        private Task<T> Create<T>(ulong serverId)
            where T : IServerSettings, new()
        {
            var s = new T();
            s.ServerId = serverId;
            return Task.FromResult(s);
        }

        private Task UpsertSettings<T>(ulong id, T value)
        {
            return _database
                .GetCollection<T>(typeof(T).Name)
                .ReplaceOneAsync(Builders<T>.Filter.Eq("_id", unchecked((long)id)), value, new ReplaceOptions() { IsUpsert = true });
        }
    }
}
