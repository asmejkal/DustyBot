using DustyBot.Database.Mongo.Collections.Templates;
using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Collections
{
    public class UserNotificationSettings : BaseUserSettings
    {
        public bool IgnoreActiveChannel { get; set; }

        public HashSet<ulong> BlockedUsers { get; set; } = new HashSet<ulong>();
    }
}
