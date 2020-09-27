using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings
{
    public class LastFmUserSettings : BaseUserSettings
    {
        public string LastFmUsername { get; set; }
        public bool Anonymous { get; set; }
    }
}
