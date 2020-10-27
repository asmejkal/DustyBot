using DustyBot.Framework.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Framework.Config
{
    public class FrameworkConfig
    {
        public string DefaultCommandPrefix { get; }
        public string BotToken { get; }
        public IReadOnlyList<ulong> OwnerIDs { get; }

        public FrameworkConfig(string defaultCommandPrefix, string botToken, IEnumerable<ulong> ownerIDs)
        {
            DefaultCommandPrefix = defaultCommandPrefix;
            BotToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
            OwnerIDs = ownerIDs?.ToList() ?? throw new ArgumentNullException(nameof(ownerIDs));
        }
    }
}
