using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Framework.Config
{
    public class FrameworkConfig
    {
        public string CommandPrefix { get; }
        public string BotToken { get; }
        public IReadOnlyList<ulong> OwnerIDs { get; }

        public FrameworkConfig(string commandPrefix, string botToken, IEnumerable<ulong> ownerIDs)
        {
            CommandPrefix = commandPrefix ?? throw new ArgumentNullException(nameof(commandPrefix));
            BotToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
            OwnerIDs = ownerIDs?.ToList() ?? throw new ArgumentNullException(nameof(ownerIDs));
        }
    }
}
