using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Settings
{
    public interface ISettingsProvider
    {
        Task<T> Get<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings;

        Task Save<T>(T settings)
            where T : IServerSettings;
    }
}
