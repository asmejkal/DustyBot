using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.LiteDB;
using DustyBot.Framework.Settings;

namespace DustyBot.Settings.LiteDB
{
    public class SettingsFactory : ISettingsFactory
    {
        public Task<T> Create<T>() 
            where T : IServerSettings
        {
            IServerSettings result;

            if (typeof(T) == typeof(MediaSettings))
                result = new MediaSettings();
            else if (typeof(T) == typeof(RolesSettings))
                result = new RolesSettings();
            else if (typeof(T) == typeof(LogSettings))
                result = new LogSettings();
            else
                throw new InvalidOperationException("Unknown settings type.");

            return Task.FromResult((T)result);
        }
    }
}
