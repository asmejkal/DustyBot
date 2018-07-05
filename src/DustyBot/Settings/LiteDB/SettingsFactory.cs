using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.LiteDB;
using DustyBot.Framework.Settings;

namespace DustyBot.Settings.LiteDB
{
    /// <summary>
    /// Can be used to supply parameters to settings constructors. Also gates settings types.
    /// </summary>
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
            else if (typeof(T) == typeof(PollSettings))
                result = new PollSettings();
            else
                throw new InvalidOperationException("Unknown settings type.");

            return Task.FromResult((T)result);
        }

        public Task<T> CreateUser<T>() 
            where T : IUserSettings
        {
            IUserSettings result;

            if (typeof(T) == typeof(UserCredentials))
                result = new UserCredentials();
            else
                throw new InvalidOperationException("Unknown settings type.");

            return Task.FromResult((T)result);
        }
    }
}
