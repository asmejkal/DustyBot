using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;

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
