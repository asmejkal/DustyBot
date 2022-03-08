using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Collections.Notifications
{
    public class UserNotificationSettings : BaseUserSettings
    {
        public bool IgnoreActiveChannel { get; set; }
        public bool OptedOut { get; set; }

        public HashSet<ulong> BlockedUsers { get; set; } = new();
    }
}
