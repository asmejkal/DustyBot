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
    public class ComebackInfo
    {
        public string Name { get; set; }
        public HashSet<string> VideoIds { get; set; } = new HashSet<string>();
        public string Category { get; set; }
    }

    public class DaumCafeFeed
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string CafeId { get; set; }
        public string BoardId { get; set; }
        public uint LastPostId { get; set; }
        public ulong TargetChannel { get; set; }
    }

    public class MediaSettings : ServerSettings
    {
        public List<ComebackInfo> YouTubeComebacks { get; set; } = new List<ComebackInfo>();
        public HashSet<ulong> ScheduleMessages { get; set; } = new HashSet<ulong>();
        public ulong ScheduleChannel { get; set; }
        public List<DaumCafeFeed> DaumCafeFeeds { get; set; } = new List<DaumCafeFeed>();
    }
}
