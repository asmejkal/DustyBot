using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Framework.LiteDB;
using static DustyBot.Helpers.GoogleHelpers;

namespace DustyBot.Settings
{
    public class BotConfig : BaseSettings, Framework.Config.IEssentialConfig
    {
        public string CommandPrefix { get; set; }
        public string BotToken { get; set; }
        public List<ulong> OwnerIDs { get; set; } = new List<ulong>();
        public string YouTubeKey { get; set; }
        public RawServiceAccountCredential GCalendarSAC { get; set; }
        public string ShortenerKey { get; set; }
        public string LastFmKey { get; set; }
        public string SpotifyId { get; set; }
        public string SpotifyKey { get; set; }
    }
}
