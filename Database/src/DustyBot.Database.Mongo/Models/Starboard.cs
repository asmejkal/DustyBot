using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Models
{
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

        private uint _threshold = DefaultThreshold;
    }
}
