using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Settings
{
    interface ILogSettings : Framework.Settings.IServerSettings
    {
        ulong EventNameChangedChannel { get; set; }
        ulong EventMessageDeletedChannel { get; set; }
        string EventMessageDeletedFilter { get; set; }
        List<ulong> EventMessageDeletedChannelFilter { get; set; }
    }
}
