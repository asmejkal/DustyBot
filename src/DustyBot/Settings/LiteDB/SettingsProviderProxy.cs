using DustyBot.Framework.LiteDB;
using DustyBot.Framework.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Settings.LiteDB
{
    //TODO: bandaid fix, convoluted
    public class SettingsProviderProxy : ISettingsProvider
    {
        SettingsProvider _provider;

        public SettingsProviderProxy(string dbPath, ISettingsFactory factory)
        {
            _provider = new SettingsProvider(dbPath, factory);
        }

        public async Task<T> Get<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings
        {
            IServerSettings result;

            if (typeof(T) == typeof(IMediaSettings))
                result = await _provider.Get<MediaSettings>(serverId, createIfNeeded);
            else if (typeof(T) == typeof(IRolesSettings))
                result = await _provider.Get<RolesSettings>(serverId, createIfNeeded);
            else if (typeof(T) == typeof(ILogSettings))
                result = await _provider.Get<LogSettings>(serverId, createIfNeeded);
            else
                throw new InvalidOperationException("Unknown settings type.");

            return (T)result;
        }

        public async Task Save<T>(T settings)
            where T : IServerSettings
        {
            IServerSettings serverSettings = settings;

            if (typeof(T) == typeof(IMediaSettings))
                await _provider.Save((MediaSettings)serverSettings);
            else if (typeof(T) == typeof(IRolesSettings))
                await _provider.Save((RolesSettings)serverSettings);
            else if (typeof(T) == typeof(ILogSettings))
                await _provider.Save((LogSettings)serverSettings);
            else
                throw new InvalidOperationException("Unknown settings type.");
        }
    }
}
