using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Core.Async;
using DustyBot.Database.Core.Settings;
using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Driver;

namespace DustyBot.Database.Services
{
    public sealed class MongoSettingsService : ISettingsService, IDisposable
    {
        private readonly KeyedSemaphoreSlim<(Type Type, ulong GuildId)> _serverSettingsMutex = new KeyedSemaphoreSlim<(Type Type, ulong GuildId)>(1, maxPoolSize: int.MaxValue);
        private readonly KeyedSemaphoreSlim<Type> _globalSettingsMutex = new KeyedSemaphoreSlim<Type>(1, maxPoolSize: int.MaxValue);

        private readonly IMongoDatabase _database;

        public MongoSettingsService(IMongoDatabase database)
        {
            _database = database;
        }

        public Task<T> Read<T>(ulong serverId, bool createIfNeeded = true, CancellationToken ct = default)
            where T : IServerSettings, new()
        {
            return SafeGetDocument(unchecked((long)serverId), _serverSettingsMutex, (typeof(T), serverId), () => Create<T>(serverId), createIfNeeded, ct);
        }

        public async Task<IEnumerable<T>> Read<T>(CancellationToken ct = default)
            where T : IServerSettings
        {
            var cursor = await _database.GetCollection<T>(typeof(T).Name).Find(Builders<T>.Filter.Empty).ToCursorAsync(ct);
            return cursor.ToEnumerable();
        }

        public async Task Modify<T>(ulong serverId, Action<T> action, CancellationToken ct = default)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId), ct: ct);
                action(settings);
                await UpsertSettings(serverId, settings, ct);
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, U> action, CancellationToken ct = default)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId), ct: ct);
                var result = action(settings);
                await UpsertSettings(serverId, settings, ct);

                return result;
            }
        }

        public async Task Modify<T>(ulong serverId, Func<T, Task> action, CancellationToken ct = default)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId), ct: ct);
                await action(settings);
                await UpsertSettings(serverId, settings, ct);
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action, CancellationToken ct = default)
            where T : IServerSettings, new()
        {
            using (await _serverSettingsMutex.ClaimAsync((typeof(T), serverId)))
            {
                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId), ct: ct);
                var result = await action(settings);
                await UpsertSettings(serverId, settings, ct);

                return result;
            }
        }

        public async Task<T> ReadGlobal<T>(CancellationToken ct = default)
            where T : new()
        {
            return await SafeGetDocument<T, Type>(unchecked((long)CollectionConstants.GlobalSettingId), _globalSettingsMutex, typeof(T), ct: ct);
        }

        public async Task ModifyGlobal<T>(Action<T> action, CancellationToken ct = default)
            where T : new()
        {
            using (await _globalSettingsMutex.ClaimAsync(typeof(T)))
            {
                var settings = await GetDocument<T>(unchecked((long)CollectionConstants.GlobalSettingId), ct: ct);
                action(settings);
                await UpsertSettings(unchecked((long)CollectionConstants.GlobalSettingId), settings, ct);
            }
        }

        public async Task<U> ModifyGlobal<T, U>(Func<T, U> action, CancellationToken ct = default)
            where T : new()
        {
            using (await _globalSettingsMutex.ClaimAsync(typeof(T)))
            {
                var settings = await GetDocument<T>(unchecked((long)CollectionConstants.GlobalSettingId), ct: ct);
                var result = action(settings);
                await UpsertSettings(unchecked((long)CollectionConstants.GlobalSettingId), settings, ct);

                return result;
            }
        }

        public void Dispose()
        {
            _serverSettingsMutex?.Dispose();
            _globalSettingsMutex?.Dispose();
        }

        private async Task<T> GetDocument<T>(long id, Func<Task<T>> creator, bool createIfNeeded = true, CancellationToken ct = default)
        {
            var collection = _database.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync(ct);
            if (settings == null && createIfNeeded)
            {
                // Create
                settings = await creator();
                await collection.InsertOneAsync(settings, cancellationToken: ct);
            }

            return settings;
        }

        private async Task<T> GetDocument<T>(long id, bool createIfNeeded = true, CancellationToken ct = default)
            where T : new()
        {
            return await GetDocument(id, () => Task.FromResult(new T()), createIfNeeded, ct);
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(long id, KeyedSemaphoreSlim<TMutexKey> mutex, TMutexKey mutexKey, Func<Task<T>> creator, bool createIfNeeded = true, CancellationToken ct = default)
            where TMutexKey : notnull
        {
            var collection = _database.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync(ct);
            if (settings == null && createIfNeeded)
            {
                // Create
                using (await mutex.ClaimAsync(mutexKey, ct))
                {
                    collection = _database.GetCollection<T>(typeof(T).Name);
                    settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync(ct);
                    if (settings == null)
                    {
                        settings = await creator();
                        await collection.InsertOneAsync(settings, cancellationToken: ct);
                    }
                }
            }

            return settings;
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(long id, KeyedSemaphoreSlim<TMutexKey> mutex, TMutexKey mutexKey, bool createIfNeeded = true, CancellationToken ct = default)
            where T : new() 
            where TMutexKey : notnull
        {
            return await SafeGetDocument(id, mutex, mutexKey, () => Task.FromResult(new T()), createIfNeeded, ct);
        }

        private Task<T> Create<T>(ulong serverId)
            where T : IServerSettings, new()
        {
            var s = new T();
            s.ServerId = serverId;
            return Task.FromResult(s);
        }

        private Task UpsertSettings<T>(ulong id, T value, CancellationToken ct = default)
        {
            return _database
                .GetCollection<T>(typeof(T).Name)
                .ReplaceOneAsync(Builders<T>.Filter.Eq("_id", unchecked((long)id)), value, new ReplaceOptions() { IsUpsert = true }, ct);
        }
    }
}
