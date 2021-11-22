using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Collections
{
    public class UserNotificationSettings : BaseUserSettings
    {
        public bool IgnoreActiveChannel { get; set; }

        public HashSet<ulong> BlockedUsers { get; set; } = new HashSet<ulong>();
    }
}
