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
        public int StarCount { get; set; }
        public ulong Author { get; set; }
        public ulong StarboardMessage { get; set; }

        public List<string> Attachments { get; set; }
    }

    public class Starboard
    {
        public const uint DefaultThreshold = 1;
        public static readonly List<string> DefaultEmojis = new List<string>() { "⭐" };

        public int Id { get; set; }
        public ulong Channel { get; set; }
        public List<string> Emojis { get; set; } = DefaultEmojis;

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

        [MongoDB.Bson.Serialization.Attributes.BsonDictionaryOptions(MongoDB.Bson.Serialization.Options.DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<ulong, StarredMessage> StarredMessages { get; set; } = new Dictionary<ulong, StarredMessage>();
    }

    public class StarboardSettings : BaseServerSettings
    {
        public List<Starboard> Starboards { get; set; } = new List<Starboard>();
        public int NextId { get; set; } = 1;
    }
}
