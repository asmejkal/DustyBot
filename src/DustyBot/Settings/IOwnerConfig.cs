using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Settings
{
    interface IOwnerConfig : Framework.Config.IEssentialConfig
    {
        string BotToken { get; }
        IReadOnlyCollection<ulong> OwnerIDs { get; }
        string YouTubeKey { get; }
    }
}
