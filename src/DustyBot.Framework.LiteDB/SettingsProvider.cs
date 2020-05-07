using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Framework.LiteDB.Utility;
using LiteDB;
using System.Threading;
using Newtonsoft.Json;
using System.Linq.Expressions;
using JsonSerializer = LiteDB.JsonSerializer;

namespace DustyBot.Framework.LiteDB
{
    public class SettingsProvider : ISettingsProvider, IDisposable
    {
        private LiteDatabase _dbObject;

        //TODO: a bit convoluted, split in multiple server/user objects each managing their own subset of locks and thread-safe read/modify methods
        AsyncMutexCollection<Tuple<Type, ulong>> _serverSettingsLocks = new AsyncMutexCollection<Tuple<Type, ulong>>();
        AsyncMutexCollection<Tuple<Type, ulong>> _userSettingsLocks = new AsyncMutexCollection<Tuple<Type, ulong>>();
        AsyncMutexCollection<Type> _globalSettingsLocks = new AsyncMutexCollection<Type>();

        public SettingsProvider(string dbPath, Migrator migrator, string password = null)
        {
            _dbObject = new LiteDatabase($"Filename={dbPath}" + (string.IsNullOrEmpty(password) ? "" : $";Password={password}") + ";Upgrade=true;Collation=en-US/IgnoreCase");

            migrator.MigrateCurrent(_dbObject);
        }

        private async Task<T> GetDocument<T>(Expression<Func<T, bool>> predicate, Func<Task<T>> creator, bool createIfNeeded = true)
        {
            var collection = _dbObject.GetCollection<T>();
            var settings = collection.FindOne(predicate);
            if (settings == null && createIfNeeded)
            {
                //Create
                settings = await creator();
                collection.Insert(settings);
            }

            return settings;
        }

        private async Task<T> GetDocument<T>(Expression<Func<T, bool>> predicate, bool createIfNeeded = true)
            where T : new()
        {
            return await GetDocument(predicate, () => Task.FromResult(new T()), createIfNeeded);
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(Expression<Func<T, bool>> predicate, AsyncMutexCollection<TMutexKey> locks, TMutexKey locksKey, Func<Task<T>> creator, bool createIfNeeded = true)
        {
            var collection = _dbObject.GetCollection<T>();
            var settings = collection.FindOne(predicate);
            if (settings == null && createIfNeeded)
            {
                //Create
                var settingsLock = await locks.GetOrCreate(locksKey);

                try
                {
                    await settingsLock.WaitAsync();

                    collection = _dbObject.GetCollection<T>();
                    settings = collection.FindOne(predicate);
                    if (settings == null)
                    {
                        settings = await creator();
                        collection.Insert(settings);
                    }
                }
                finally
                {
                    settingsLock.Release();
                }
            }

            return settings;
        }

        private async Task<T> SafeGetDocument<T, TMutexKey>(Expression<Func<T, bool>> predicate, AsyncMutexCollection<TMutexKey> locks, TMutexKey locksKey, bool createIfNeeded = true) 
            where T : new()
        {
            return await SafeGetDocument(predicate, locks, locksKey, () => Task.FromResult(new T()), createIfNeeded);
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
            return SafeGetDocument(x => x.ServerId == serverId, _serverSettingsLocks, Tuple.Create(typeof(T), serverId), () => Create<T>(serverId), createIfNeeded);
        }

        public Task<IEnumerable<T>> Read<T>() 
            where T : IServerSettings
        {
            // We have to enumerate here to avoid lock issues in LiteDB... 
            // https://github.com/mbdavid/LiteDB/issues/1377
            // https://github.com/mbdavid/LiteDB/issues/1637
            return Task.FromResult<IEnumerable<T>>(_dbObject.GetCollection<T>().FindAll().ToList()); 
        }
        
        public async Task Modify<T>(ulong serverId, Action<T> action)
            where T : IServerSettings, new()
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));
            
            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(x => x.ServerId == serverId, () => Create<T>(serverId));
                action(settings);
                _dbObject.GetCollection<T>().Update(settings);
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

                var settings = await GetDocument(x => x.ServerId == serverId, () => Create<T>(serverId));
                var result = action(settings);
                _dbObject.GetCollection<T>().Update(settings);

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

                var settings = await GetDocument(x => x.ServerId == serverId, () => Create<T>(serverId));
                await action(settings);
                _dbObject.GetCollection<T>().Update(settings);
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

                var settings = await GetDocument(x => x.ServerId == serverId, () => Create<T>(serverId));
                var result = await action(settings);
                _dbObject.GetCollection<T>().Update(settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public Task<string> DumpSettings(ulong serverId, string module)
        {
            var col = _dbObject.GetCollection(module);
                
            var settings = col.FindOne(x => x.ContainsKey("ServerId") && x["ServerId"].AsUInt64() == serverId );
            if (settings == null)
                return null;

            return Task.FromResult(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(settings.ToString()), Formatting.Indented) + "\n\n");
        }

        public Task SetSettings(ulong serverId, string module, string json)
        {
            var col = _dbObject.GetCollection(module);

            var dJson = JsonSerializer.Deserialize(json);
            var doc = BsonMapper.Global.ToDocument(dJson);
            col.Upsert(doc);

            return Task.CompletedTask;
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
                foreach (var colName in _dbObject.GetCollectionNames())
                {
                    var col = _dbObject.GetCollection(colName);
                    col.DeleteMany(x => x.ContainsKey("ServerId") && x["ServerId"].AsUInt64() == serverId);
                }
            });
        }

        public async Task<T> ReadGlobal<T>() 
            where T : new()
        {
            return await SafeGetDocument<T, Type>(x => true, _globalSettingsLocks, typeof(T));
        }

        public async Task ModifyGlobal<T>(Action<T> action) 
            where T : new()
        {
            var settingsLock = await _globalSettingsLocks.GetOrCreate(typeof(T));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument<T>(x => true);
                action(settings);
                _dbObject.GetCollection<T>().Update(settings);
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
            return await SafeGetDocument(x => x.UserId == userId, _userSettingsLocks, Tuple.Create(typeof(T), userId), () => CreateUser<T>(userId), createIfNeeded);
        }

        public Task<IEnumerable<T>> ReadUser<T>() 
            where T : IUserSettings
        {
            // We have to enumerate here to avoid lock issues in LiteDB... 
            // https://github.com/mbdavid/LiteDB/issues/1377
            // https://github.com/mbdavid/LiteDB/issues/1637
            return Task.FromResult<IEnumerable<T>>(_dbObject.GetCollection<T>().FindAll().ToList());
        }

        public async Task ModifyUser<T>(ulong userId, Action<T> action) 
            where T : IUserSettings, new()
        {
            var settingsLock = await _userSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), userId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await GetDocument(x => x.UserId == userId, () => CreateUser<T>(userId));
                action(settings);
                _dbObject.GetCollection<T>().Update(settings);
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

                var settings = await GetDocument(x => x.UserId == userId, () => CreateUser<T>(userId));
                var result = action(settings);
                _dbObject.GetCollection<T>().Update(settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }
        
        #region IDisposable 

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _dbObject?.Dispose();
                    _dbObject = null;

                    _serverSettingsLocks?.Dispose();
                    _serverSettingsLocks = null;

                    _userSettingsLocks?.Dispose();
                    _userSettingsLocks = null;

                    _globalSettingsLocks?.Dispose();
                    _globalSettingsLocks = null;
                }

                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }
}
