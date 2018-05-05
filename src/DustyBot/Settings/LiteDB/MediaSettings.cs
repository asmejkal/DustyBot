using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings.LiteDB
{
    public class MediaSettings : ServerSettings, IMediaSettings
    {
        public List<ComebackInfo> YouTubeComebacks { get; set; } = new List<ComebackInfo>();
        public HashSet<ulong> ScheduleMessages { get; set; } = new HashSet<ulong>();
        public ulong ScheduleChannel { get; set; }
    }
}
