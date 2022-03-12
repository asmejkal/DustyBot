using System;
using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Notifications.Models;
using DustyBot.Database.Mongo.Collections.Templates;
using Mongo.Migration.Documents.Attributes;

namespace DustyBot.Database.Mongo.Collections.Notifications
{
    [StartUpVersion("1.1.0"), CollectionLocation(nameof(NotificationSettings))]
    public class NotificationSettings : BaseServerSettings
    {
        public List<Notification> Notifications { get; set; } = new();

        public HashSet<ulong> IgnoredUsers { get; set; } = new();

        public Dictionary<ulong, HashSet<ulong>> UserIgnoredChannels { get; set; } = new();

        public Dictionary<ulong, int> UserQuotas { get; set; } = new();
        public DateTime CurrentQuotaDate { get; set; }
    }
}
