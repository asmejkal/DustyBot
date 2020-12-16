﻿using DustyBot.Core.Async;
using DustyBot.Database.Core.Settings;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Serializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public sealed class MongoSettingsService : ISettingsService, IDisposable
    {
        public string DatabaseName { get; }

        private IMongoClient _client;
        private IMongoDatabase _db;

        //TODO: a bit convoluted, split in multiple server/user objects each managing their own subset of locks and thread-safe read/modify methods
        KeyedSemaphoreSlim<(Type Type, ulong GuildId)> _serverSettingsMutex = new KeyedSemaphoreSlim<(Type Type, ulong GuildId)>(1, maxPoolSize: int.MaxValue);
        KeyedSemaphoreSlim<(Type Type, ulong UserId)> _userSettingsMutex = new KeyedSemaphoreSlim<(Type Type, ulong UserId)>(1, maxPoolSize: int.MaxValue);
        KeyedSemaphoreSlim<Type> _globalSettingsMutex = new KeyedSemaphoreSlim<Type>(1, maxPoolSize: int.MaxValue);

        private MongoSettingsService(IMongoClient client, IMongoDatabase db, string databaseName)
        {
            _client = client;
            _db = db;
            DatabaseName = databaseName;
        }

        public static Task<MongoSettingsService> CreateAsync(string connectionString)
        {
            BsonSerializer.RegisterSerializer(DateTimeSerializer.LocalInstance);
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new SecureStringSerializer());

            var url = MongoUrl.Create(connectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);
            return Task.FromResult(new MongoSettingsService(client, db, url.DatabaseName));
        }

        private async Task<T> GetDocument<T>(long id, Func<Task<T>> creator, bool createIfNeeded = true)
        {
            var collection = _db.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
            if (settings == null && createIfNeeded)
            {
                //Create
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
            var collection = _db.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
            if (settings == null && createIfNeeded)
            {
                //Create
                using (await mutex.ClaimAsync(mutexKey))
                {
                    collection = _db.GetCollection<T>(typeof(T).Name);
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

        public Task<T> Read<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings, new()
        {
            return SafeGetDocument(unchecked((long)serverId), _serverSettingsMutex, (typeof(T), serverId), () => Create<T>(serverId), createIfNeeded);
        }

        public async Task<IEnumerable<T>> Read<T>()
            where T : IServerSettings
        {
            var cursor = await _db.GetCollection<T>(typeof(T).Name).Find(Builders<T>.Filter.Empty).ToCursorAsync();
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

        private Task<T> CreateUser<T>(ulong userId)
            where T : IUserSettings, new()
        {
            var s = new T();
            s.UserId = userId;
            return Task.FromResult(s);
        }

        public async Task<T> ReadUser<T>(ulong userId, bool createIfNeeded = true)
            where T : IUserSettings, new()
        {
            return await SafeGetDocument(unchecked((long)userId), _userSettingsMutex, (typeof(T), userId), () => CreateUser<T>(userId), createIfNeeded);
        }

        public async Task<IEnumerable<T>> ReadUser<T>()
            where T : IUserSettings
        {
            var cursor = await _db.GetCollection<T>(typeof(T).Name).Find(Builders<T>.Filter.Empty).ToCursorAsync();
            return cursor.ToEnumerable();
        }

        public async Task ModifyUser<T>(ulong userId, Action<T> action)
            where T : IUserSettings, new()
        {
            using (await _userSettingsMutex.ClaimAsync((typeof(T), userId)))
            {
                var settings = await GetDocument(unchecked((long)userId), () => CreateUser<T>(userId));
                action(settings);
                await UpsertSettings(userId, settings);
            }
        }

        public async Task<U> ModifyUser<T, U>(ulong userId, Func<T, U> action)
            where T : IUserSettings, new()
        {
            using (await _userSettingsMutex.ClaimAsync((typeof(T), userId)))
            {
                var settings = await GetDocument(unchecked((long)userId), () => CreateUser<T>(userId));
                var result = action(settings);
                await UpsertSettings(userId, settings);

                return result;
            }
        }

        private Task UpsertSettings<T>(ulong id, T value)
        {
            return _db
                .GetCollection<T>(typeof(T).Name)
                .ReplaceOneAsync(Builders<T>.Filter.Eq("_id", unchecked((long)id)), value, new ReplaceOptions() { IsUpsert = true });
        }

        public void Dispose()
        {
            _serverSettingsMutex?.Dispose();
            _serverSettingsMutex = null;

            _userSettingsMutex?.Dispose();
            _userSettingsMutex = null;

            _globalSettingsMutex?.Dispose();
            _globalSettingsMutex = null;
        }
    }
}
