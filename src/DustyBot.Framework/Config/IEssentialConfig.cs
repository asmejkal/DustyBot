using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Config
{
    public interface IEssentialConfig
    {
        string CommandPrefix { get; }
        string BotToken { get; }
        List<ulong> OwnerIDs { get; }
    }
}
