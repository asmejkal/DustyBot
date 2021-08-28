using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Collections
{
    public class AdministrationSettings : BaseServerSettings
    {
        public string AutobanUsernameRegex { get; set; }
        public ulong AutobanLogChannelId { get; set; }
    }
}
