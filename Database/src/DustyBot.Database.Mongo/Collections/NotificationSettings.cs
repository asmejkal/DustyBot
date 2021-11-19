using System;
using System.Collections.Generic;
using System.Linq;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Collections
{
    public class NotificationSettings : BaseServerSettings
    {
        public List<Notification> Notifications { get; set; } = new List<Notification>();

        public HashSet<ulong> IgnoredUsers { get; set; } = new HashSet<ulong>();

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<ulong, HashSet<ulong>> UserIgnoredChannels { get; set; } = new Dictionary<ulong, HashSet<ulong>>();

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<ulong, int> UserQuotas { get; set; } = new Dictionary<ulong, int>();
        public DateTime CurrentQuotaDate { get; set; }

        public bool RaiseCount(ulong user, string loweredWord)
        {
            var n = Notifications.FirstOrDefault(x => x.User == user && x.LoweredWord == loweredWord);
            if (n != null)
                n.TriggerCount++;

            return n != null;
        }
    }
}
