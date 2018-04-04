using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;

namespace DustyBot.Framework.LiteDB
{
    public interface ISettingsFactory
    {
        Task<T> Create<T>()
            where T : IServerSettings;
    }
}
