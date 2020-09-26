using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;
using DustyBot.Framework.LiteDB.Utility;
using Newtonsoft.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using DustyBot.Database.Mongo.Serializers;

namespace DustyBot.Framework.LiteDB
{
    public sealed class MongoDbSettingsProvider : ISettingsProvider, IDisposable
    {
        private MongoClient _client;
        private IMongoDatabase _db;

        //TODO: a bit convoluted, split in multiple server/user objects each managing their own subset of locks and thread-safe read/modify methods
        AsyncMutexCollection<Tuple<Type, ulong>> _serverSettingsLocks = new AsyncMutexCollection<Tuple<Type, ulong>>();
        AsyncMutexCollection<Tuple<Type, ulong>> _userSettingsLocks = new AsyncMutexCollection<Tuple<Type, ulong>>();
        AsyncMutexCollection<Type> _globalSettingsLocks = new AsyncMutexCollection<Type>();

        public MongoDbSettingsProvider(string connectionString)
        {
            BsonSerializer.RegisterSerializer(DateTimeSerializer.LocalInstance);
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new SecureStringSerializer());

            var url = MongoUrl.Create(connectionString);
            _client = new MongoClient(url);
            _db = _client.GetDatabase(url.DatabaseName);
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

        private async Task<T> SafeGetDocument<T, TMutexKey>(long id, AsyncMutexCollection<TMutexKey> locks, TMutexKey locksKey, Func<Task<T>> creator, bool createIfNeeded = true)
        {
            var collection = _db.GetCollection<T>(typeof(T).Name);
            var settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
            if (settings == null && createIfNeeded)
            {
                //Create
                var settingsLock = await locks.GetOrCreate(locksKey);

                try
                {
                    await settingsLock.WaitAsync();

                    collection = _db.GetCollection<T>(typeof(T).Name);
                    settings = await collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
                    if (settings == null)
                    {
                        settings = await creator();
                        await collection.InsertOneAsync(settings);
                    }
                }
                finally
                {
                    settingsLock.Release();
                }
            }

            return settings;
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(long id, AsyncMutexCollection<TMutexKey> locks, TMutexKey locksKey, bool createIfNeeded = true) 
            where T : new()
        {
            return await SafeGetDocument(id, locks, locksKey, () => Task.FromResult(new T()), createIfNeeded);
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
            return SafeGetDocument(unchecked((long)serverId), _serverSettingsLocks, Tuple.Create(typeof(T), serverId), () => Create<T>(serverId), createIfNeeded);
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
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));
            
            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                action(settings);
                await UpsertSettings(serverId, settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, U> action)
            where T : IServerSettings, new()
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                var result = action(settings);
                await UpsertSettings(serverId, settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task Modify<T>(ulong serverId, Func<T, Task> action)
            where T : IServerSettings, new()
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                await action(settings);
                await UpsertSettings(serverId, settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action)
            where T : IServerSettings, new()
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(unchecked((long)serverId), () => Create<T>(serverId));
                var result = await action(settings);
                await UpsertSettings(serverId, settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<string> DumpSettings(ulong serverId, string module, bool raw)
        {
            var col = _db.GetCollection<BsonDocument>(module);

            var settings = await col.Find(Builders<BsonDocument>.Filter.Eq("_id", unchecked((long)serverId))).FirstOrDefaultAsync();
            if (settings == null)
                return null;

            if (raw)
                return settings.ToString();
            else
                return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(settings.ToString()), Formatting.Indented) + "\n\n";
        }

        public async Task SetSettings(string module, string json)
        {
            var col = _db.GetCollection<BsonDocument>(module);

            var doc = BsonDocument.Parse(json);
            await col.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]), doc);
        }

        public async Task DeleteServer(ulong serverId)
        {
            await _serverSettingsLocks.InterlockedModify(async locks =>
            {
                //Wait for all settings locks for this server to get released and dispose them
                var serverLocks = locks.Where(k => k.Key.Item2 == serverId).ToList();
                foreach (var l in serverLocks)
                {
                    await l.Value.WaitAsync();

                    locks.Remove(l.Key);
                    l.Value.Dispose();
                }

                //Remove all settings
                foreach (var colName in (await _db.ListCollectionNamesAsync()).ToEnumerable())
                {
                    var col = _db.GetCollection<BsonDocument>(colName);
                    await col.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", unchecked((long)serverId)));
                }
            });
        }

        public async Task<T> ReadGlobal<T>() 
            where T : new()
        {
            return await SafeGetDocument<T, Type>(Definitions.GlobalSettingsId, _globalSettingsLocks, typeof(T));
        }

        public async Task ModifyGlobal<T>(Action<T> action) 
            where T : new()
        {
            var settingsLock = await _globalSettingsLocks.GetOrCreate(typeof(T));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument<T>(Definitions.GlobalSettingsId);
                action(settings);
                await UpsertSettings(Definitions.GlobalSettingsId, settings);
            }
            finally
            {
                settingsLock.Release();
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
            return await SafeGetDocument(unchecked((long)userId), _userSettingsLocks, Tuple.Create(typeof(T), userId), () => CreateUser<T>(userId), createIfNeeded);
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
            var settingsLock = await _userSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), userId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(unchecked((long)userId), () => CreateUser<T>(userId));
                action(settings);
                await UpsertSettings(userId, settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<U> ModifyUser<T, U>(ulong userId, Func<T, U> action) 
            where T : IUserSettings, new()
        {
            var settingsLock = await _userSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), userId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(unchecked((long)userId), () => CreateUser<T>(userId));
                var result = action(settings);
                await UpsertSettings(userId, settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task Set<T>(T settings)
            where T : IServerSettings
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), settings.ServerId));

            try
            {
                await settingsLock.WaitAsync();

                await UpsertSettings(settings.ServerId, settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task SetUser<T>(T settings)
            where T : IUserSettings
        {
            var settingsLock = await _userSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), settings.UserId));

            try
            {
                await settingsLock.WaitAsync();

                await UpsertSettings(settings.UserId, settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task SetGlobal<T>(T settings)
        {
            var settingsLock = await _globalSettingsLocks.GetOrCreate(typeof(T));

            try
            {
                await settingsLock.WaitAsync();

                await UpsertSettings(Definitions.GlobalSettingsId, settings);
            }
            finally
            {
                settingsLock.Release();
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
            _serverSettingsLocks?.Dispose();
            _serverSettingsLocks = null;

            _userSettingsLocks?.Dispose();
            _userSettingsLocks = null;

            _globalSettingsLocks?.Dispose();
            _globalSettingsLocks = null;
        }
    }
}
