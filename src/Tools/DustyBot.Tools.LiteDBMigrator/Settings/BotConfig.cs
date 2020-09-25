using System.Collections.Generic;
using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings
{
    public class BotConfig : BaseGlobalSettings
    {
        public string CommandPrefix { get; set; }
        public string BotToken { get; set; }
        public List<ulong> OwnerIDs { get; set; } = new List<ulong>();
        public string YouTubeKey { get; set; }
        public string ShortenerKey { get; set; }
        public string LastFmKey { get; set; }
        public string SpotifyId { get; set; }
        public string SpotifyKey { get; set; }
        public string TableStorageConnectionString { get; set; }
        public string SqlDbConnectionString { get; set; }
        public string PapagoClientId { get; set; }
        public string PapagoClientSecret { get; set; }
    }
}
