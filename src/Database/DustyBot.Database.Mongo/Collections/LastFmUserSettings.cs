using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Settings
{
    public class LastFmUserSettings : BaseUserSettings
    {
        public string LastFmUsername { get; set; }
        public bool Anonymous { get; set; }
    }
}
