using System;
using Disqord.Bot;
using Disqord.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace DustyBot.Framework.Commands
{
    public class DustyCommandContext : DiscordCommandContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();

        public DustyCommandContext(DiscordBotBase bot, IPrefix prefix, string input, IGatewayUserMessage message, IServiceScope serviceScope) 
            : base(bot, prefix, input, message, serviceScope)
        {
        }
    }
}
