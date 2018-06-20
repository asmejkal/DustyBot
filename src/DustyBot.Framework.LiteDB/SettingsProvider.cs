using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;
using LiteDB;
using System.Threading;

namespace DustyBot.Framework.LiteDB
{
    public class SettingsProvider : ISettingsProvider
    {
        public LiteDatabase DbObject { get; private set; }
        SemaphoreSlim _dbOjectLock = new SemaphoreSlim(1, 1);

        private ISettingsFactory _factory;

        public SettingsProvider(string dbPath, ISettingsFactory factory, Migrator migrator, string password = null)
        {
            _factory = factory;
            
            DbObject = new LiteDatabase($"Filename={dbPath}" + (string.IsNullOrEmpty(password) ? "" : $";Password={password}"));
            migrator.MigrateCurrent(DbObject);
        }

        public async Task<T> Read<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings
        {
            var collection = DbObject.GetCollection<T>();
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

        public Task<IEnumerable<T>> Read<T>() where T : IServerSettings
        {
            return Task.FromResult(DbObject.GetCollection<T>().FindAll());
        }

        private Task Save<T>(T settings) 
            where T : IServerSettings
        {
            DbObject.GetCollection<T>().Upsert(settings);
            return Task.CompletedTask;
        }

        Dictionary<Tuple<Type, ulong>, SemaphoreSlim> _serverSettingsLocks = new Dictionary<Tuple<Type, ulong>, SemaphoreSlim>();
        
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
                await Save(settings);
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
                await Save(settings);

                return result;
            }
            finally
            {
                settingsLock.Release();
            }
        }
    }
}
