using DustyBot.Database.Mongo.Collections.Templates;
using System.Collections.Generic;

namespace DustyBot.Settings
{
    public class LogSettings : BaseServerSettings
    {
        public ulong EventNameChangedChannel { get; set; }
        public ulong EventMessageDeletedChannel { get; set; }
        public string EventMessageDeletedFilter { get; set; }
        public List<ulong> EventMessageDeletedChannelFilter { get; set; } = new List<ulong>();
    }
}
