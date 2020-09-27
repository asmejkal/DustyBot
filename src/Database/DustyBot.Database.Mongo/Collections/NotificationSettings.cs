﻿using DustyBot.Database.Mongo.Collections.Templates;
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

        public bool RaiseCount(ulong user, string loweredWord)
        {
            var n = Notifications.FirstOrDefault(x => x.User == user && x.LoweredWord == loweredWord);
            if (n != null)
                n.TriggerCount++;

            return n != null;
        }
    }
}
