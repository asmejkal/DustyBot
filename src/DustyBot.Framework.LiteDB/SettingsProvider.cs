using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;
using LiteDB;
using System.Threading;
using Newtonsoft.Json;

namespace DustyBot.Framework.LiteDB
{
    public class SettingsProvider : ISettingsProvider, IDisposable
    {
        private LiteDatabase _dbObject;

        private ISettingsFactory _factory;
        Dictionary<Tuple<Type, ulong>, SemaphoreSlim> _serverSettingsLocks = new Dictionary<Tuple<Type, ulong>, SemaphoreSlim>();
        Dictionary<Tuple<Type, ulong>, SemaphoreSlim> _userSettingsLocks = new Dictionary<Tuple<Type, ulong>, SemaphoreSlim>();
        Dictionary<Type, SemaphoreSlim> _globalSettingsLocks = new Dictionary<Type, SemaphoreSlim>();

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
                settings = await _factory.Create<T>();
                settings.ServerId = serverId;

                collection.Insert(settings);
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
            SemaphoreSlim settingsLock;
            lock (_serverSettingsLocks)
            {
                var key = Tuple.Create(typeof(T), serverId);
                if (!_serverSettingsLocks.TryGetValue(key, out settingsLock))
                    _serverSettingsLocks.Add(key, settingsLock = new SemaphoreSlim(1, 1));
            }
            
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
            SemaphoreSlim settingsLock;
            lock (_serverSettingsLocks)
            {
                var key = Tuple.Create(typeof(T), serverId);
                if (!_serverSettingsLocks.TryGetValue(key, out settingsLock))
                    _serverSettingsLocks.Add(key, settingsLock = new SemaphoreSlim(1, 1));
            }

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
                result += colName + ":\n";
                var col = _dbObject.GetCollection(colName);
                
                var settings = col.FindOne(x => unchecked((UInt64)((Int64)x["ServerId"].RawValue)) == serverId );
                if (settings == null)
                    continue;
                
                result += JsonConvert.SerializeObject(JsonConvert.DeserializeObject(settings.ToString()), Formatting.Indented) + "\n\n";
            }

            return Task.FromResult(result);
        }

        public Task<T> ReadGlobal<T>() 
            where T : new()
        {
            var collection = _dbObject.GetCollection<T>();
            T settings;
            if (collection.Count() <= 0)
            {
                settings = new T();
                collection.Insert(settings);
            }
            else
                settings = collection.FindOne(Query.All());

            return Task.FromResult(settings);
        }

        public async Task ModifyGlobal<T>(Action<T> action) 
            where T : new()
        {
            SemaphoreSlim settingsLock;
            lock (_globalSettingsLocks)
            {
                if (!_globalSettingsLocks.TryGetValue(typeof(T), out settingsLock))
                    _globalSettingsLocks.Add(typeof(T), settingsLock = new SemaphoreSlim(1, 1));
            }

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
                settings = await _factory.CreateUser<T>();
                settings.UserId = userId;

                collection.Insert(settings);
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
            SemaphoreSlim settingsLock;
            lock (_userSettingsLocks)
            {
                var key = Tuple.Create(typeof(T), userId);
                if (!_userSettingsLocks.TryGetValue(key, out settingsLock))
                    _userSettingsLocks.Add(key, settingsLock = new SemaphoreSlim(1, 1));
            }

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
            SemaphoreSlim settingsLock;
            lock (_userSettingsLocks)
            {
                var key = Tuple.Create(typeof(T), userId);
                if (!_userSettingsLocks.TryGetValue(key, out settingsLock))
                    _userSettingsLocks.Add(key, settingsLock = new SemaphoreSlim(1, 1));
            }

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
