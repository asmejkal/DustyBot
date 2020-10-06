using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;

namespace DustyBot.Settings
{
    public enum StarboardStyle
    {
        Text,
        Embed
    }

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
        public StarboardStyle Style { get; set; }
        public bool AllowSelfStars { get; set; }
        public bool KeepUnstarred { get; set; }
        public bool KeepDeleted { get; set; }

        uint _threshold = DefaultThreshold;
        public uint Threshold
        {
            get => _threshold;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException();

                _threshold = value;
            }
        }

        public HashSet<ulong> ChannelsWhitelist { get; set; } = new HashSet<ulong>();

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<ulong, StarredMessage> StarredMessages { get; set; } = new Dictionary<ulong, StarredMessage>();
    }

    public class StarboardSettings : BaseServerSettings
    {
        public List<Starboard> Starboards { get; set; } = new List<Starboard>();
        public int NextId { get; set; } = 1;
    }
}
