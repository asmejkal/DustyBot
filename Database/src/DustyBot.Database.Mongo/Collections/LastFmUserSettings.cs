using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Collections
{
    public class LastFmUserSettings : BaseUserSettings
    {
        public string? LastFmUsername { get; set; }
        public bool Anonymous { get; set; }
    }
}
