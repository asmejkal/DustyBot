using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Settings
{
    public class UserNotificationSettings : BaseUserSettings
    {
        public bool IgnoreActiveChannel { get; set; }
    }
}
