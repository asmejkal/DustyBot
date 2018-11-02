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
    public class StarredMessage
    {
        public HashSet<ulong> Starrers { get; set; } = new HashSet<ulong>();
        public ulong Author { get; set; }
        public ulong StarboardMessage { get; set; }

        public List<string> Attachments { get; set; }
    }

    public class Starboard
    {
        public const uint DefaultThreshold = 1;
        public const string DefaultEmoji = "⭐";

        public int Id { get; set; }
        public ulong Channel { get; set; }
        public string Emoji { get; set; } = DefaultEmoji;

        uint _threshold = DefaultThreshold;
        public uint Threshold
        {
            get => _threshold;
            set
            {
                if (value > 99 || value < 1)
                    throw new ArgumentOutOfRangeException();

                _threshold = value;
            }
        }

        public HashSet<ulong> ChannelsWhitelist { get; set; } = new HashSet<ulong>();

        public Dictionary<ulong, StarredMessage> StarredMessages { get; set; } = new Dictionary<ulong, StarredMessage>();
    }

    public class StarboardSettings : BaseServerSettings
    {
        public List<Starboard> Starboards { get; set; } = new List<Starboard>();
        public int NextId { get; set; } = 1;
    }
}
