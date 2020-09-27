using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Settings
{
    public interface IServerSettings
    {
        ulong ServerId { get; set; }
    }
}
