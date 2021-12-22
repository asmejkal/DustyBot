using System;
using Disqord.Bot;
using Disqord.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace DustyBot.Framework.Commands
{
    public class DustyGuildCommandContext : DiscordGuildCommandContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();

        public DustyGuildCommandContext(
            DiscordBotBase bot,
            IPrefix prefix,
            string input,
            IGatewayUserMessage message,
            CachedMessageGuildChannel channel,
            IServiceScope serviceScope) 
            : base(bot, prefix, input, message, channel, serviceScope)
        {
        }
    }
}
