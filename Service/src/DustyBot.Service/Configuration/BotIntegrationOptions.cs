using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Service.Configuration
{
    public class BotIntegrationOptions
    {
        public string AllowedInteractionBotIdsList { get; set; }

        public IReadOnlyCollection<ulong> AllowedInteractionBotIds => AllowedInteractionBotIdsList?.Split(',').Select(x => ulong.Parse(x.Trim())).ToList();
    }
}
