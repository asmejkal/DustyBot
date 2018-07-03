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

namespace DustyBot.Framework.LiteDB
{
    public class SettingsProvider : ISettingsProvider, IDisposable
    {
        private LiteDatabase _dbObject;

        private ISettingsFactory _factory;

        //TODO: a bit convoluted, split in multiple server/user objects each managing their own subset of locks and thread-safe read/modify methods
        AsyncMutexCollection<Tuple<Type, ulong>> _serverSettingsLocks = new AsyncMutexCollection<Tuple<Type, ulong>>();
        AsyncMutexCollection<Tuple<Type, ulong>> _userSettingsLocks = new AsyncMutexCollection<Tuple<Type, ulong>>();
        AsyncMutexCollection<Type> _globalSettingsLocks = new AsyncMutexCollection<Type>();

        public SettingsProvider(string dbPath, ISettingsFactory factory, Migrator migrator, string password = null)
        {
            _factory = factory;

            _dbObject = new LiteDatabase($"Filename={dbPath}" + (string.IsNullOrEmpty(password) ? "" : $";Password={password}"));
            migrator.MigrateCurrent(_dbObject);
        }

        public async Task<T> Read<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings
        {
            var collection = _dbObject.GetCollection<T>();
            var settings = collection.FindOne(x => x.ServerId == serverId);
            if (settings == null && createIfNeeded)
            {
                //Create
                var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));

                try
                {
                    await settingsLock.WaitAsync();

                    collection = _dbObject.GetCollection<T>();
                    settings = collection.FindOne(x => x.ServerId == serverId);
                    if (settings == null)
                    {
                        settings = await _factory.Create<T>();
                        settings.ServerId = serverId;

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

        public Task<IEnumerable<T>> Read<T>() 
            where T : IServerSettings
        {
            return Task.FromResult(_dbObject.GetCollection<T>().FindAll());
        }
        
        public async Task Modify<T>(ulong serverId, Action<T> action)
            where T : IServerSettings
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));
            
            try
            {
                await settingsLock.WaitAsync();

                var settings = await Read<T>(serverId);
                action(settings);
                _dbObject.GetCollection<T>().Update(settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<U> Modify<T, U>(ulong serverId, Func<T, U> action)
            where T : IServerSettings
        {
            var settingsLock = await _serverSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), serverId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await Read<T>(serverId);
                var result = action(settings);
                _dbObject.GetCollection<T>().Update(settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public Task<string> DumpSettings(ulong serverId)
        {
            string result = "";
            foreach (var colName in _dbObject.GetCollectionNames())
            {
                var col = _dbObject.GetCollection(colName);
                
                var settings = col.FindOne(x => x.ContainsKey("ServerId") && unchecked((UInt64)((Int64)x["ServerId"].RawValue)) == serverId );
                if (settings == null)
                    continue;

                result += colName + ":\n";
                result += JsonConvert.SerializeObject(JsonConvert.DeserializeObject(settings.ToString()), Formatting.Indented) + "\n\n";
            }

            return Task.FromResult(result);
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
                    col.Delete(x => x.ContainsKey("ServerId") && unchecked((UInt64)((Int64)x["ServerId"].RawValue)) == serverId);
                }
            });
        }

        public async Task<T> ReadGlobal<T>() 
            where T : new()
        {
            var collection = _dbObject.GetCollection<T>();
            var settings = collection.FindOne(Query.All());
            if (settings == null)
            {
                //Create
                var settingsLock = await _globalSettingsLocks.GetOrCreate(typeof(T));

                try
                {
                    await settingsLock.WaitAsync();

                    collection = _dbObject.GetCollection<T>();
                    settings = collection.FindOne(Query.All());
                    if (settings == null)
                    {
                        settings = new T();
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

        public async Task ModifyGlobal<T>(Action<T> action) 
            where T : new()
        {
            var settingsLock = await _globalSettingsLocks.GetOrCreate(typeof(T));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await ReadGlobal<T>();
                action(settings);
                _dbObject.GetCollection<T>().Update(settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<T> ReadUser<T>(ulong userId, bool createIfNeeded = true) 
            where T : IUserSettings
        {
            var collection = _dbObject.GetCollection<T>();
            var settings = collection.FindOne(x => x.UserId == userId);
            if (settings == null && createIfNeeded)
            {
                //Create
                var settingsLock = await _globalSettingsLocks.GetOrCreate(typeof(T));

                try
                {
                    await settingsLock.WaitAsync();

                    collection = _dbObject.GetCollection<T>();
                    settings = collection.FindOne(x => x.UserId == userId);
                    if (settings == null)
                    {
                        settings = await _factory.CreateUser<T>();
                        settings.UserId = userId;

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

        public Task<IEnumerable<T>> ReadUser<T>() 
            where T : IUserSettings
        {
            return Task.FromResult(_dbObject.GetCollection<T>().FindAll());
        }

        public async Task ModifyUser<T>(ulong userId, Action<T> action) 
            where T : IUserSettings
        {
            var settingsLock = await _userSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), userId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await ReadUser<T>(userId);
                action(settings);
                _dbObject.GetCollection<T>().Update(settings);
            }
            finally
            {
                settingsLock.Release();
            }
        }

        public async Task<U> ModifyUser<T, U>(ulong userId, Func<T, U> action) 
            where T : IUserSettings
        {
            var settingsLock = await _userSettingsLocks.GetOrCreate(Tuple.Create(typeof(T), userId));

            try
            {
                await settingsLock.WaitAsync();

                var settings = await ReadUser<T>(userId);
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
