using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Settings;
using LiteDB;

namespace DustyBot.Framework.LiteDB
{
    public abstract class BaseServerSettings : IServerSettings
    {
        [BsonId]
        public ulong ServerId { get; set; }
    }
}
