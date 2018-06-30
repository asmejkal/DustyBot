using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings
{
    public class BotConfig : BaseSettings, Framework.Config.IEssentialConfig
    {
        public string CommandPrefix { get; set; }
        public string BotToken { get; set; }
        public List<ulong> OwnerIDs { get; set; }
        public string YouTubeKey { get; set; }
    }
}
