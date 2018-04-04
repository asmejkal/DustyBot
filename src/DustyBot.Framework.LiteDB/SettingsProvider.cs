using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;
using LiteDB;

namespace DustyBot.Framework.LiteDB
{
    public class SettingsProvider : ISettingsProvider
    {
        public LiteDatabase DbObject { get; private set; }

        private ISettingsFactory _factory;

        public SettingsProvider(string dbPath, ISettingsFactory factory)
        {
            _factory = factory;

            DbObject = new LiteDatabase(dbPath);
        }

        public async Task<T> Get<T>(ulong serverId, bool createIfNeeded = true)
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

        public Task Save<T>(T settings) 
            where T : IServerSettings
        {
            DbObject.GetCollection<T>().Upsert(settings);
            return Task.CompletedTask;
        }
    }
}
