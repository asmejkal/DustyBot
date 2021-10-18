﻿using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Settings
{
    public class Notification
    {
        public string LoweredWord { get; set; }
        public string OriginalWord { get; set; }
        public ulong User { get; set; }
        public uint TriggerCount { get; set; }
    }

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
