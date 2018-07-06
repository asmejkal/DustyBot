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
    public class EventsSettings : BaseServerSettings
    {
        public ulong GreetChannel { get; set; }
        public string GreetMessage { get; set; }

        public ulong ByeChannel { get; set; }
        public string ByeMessage { get; set; }
    }
}
