using System.Collections.Generic;

namespace DustyBot.Service.Configuration
{
    public class BotIntegrationOptions
    {
        public List<ulong> AllowedInteractionBotIds { get; set; } = [];
    }
}
