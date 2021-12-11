using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Client;

namespace DustyBot.Framework.Entities
{
    public static class ChannelExtensions
    {
        public static Task<IUserMessage> SendMessageAsync(
            this IMessageGuildChannel channel,
            LocalMessage message,
            IRestRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var client = channel.GetGatewayClient();
            if (!client.CacheProvider.TryGetGuilds(out var guilds) || !guilds.TryGetValue(channel.GuildId, out var guild))
                throw new InvalidOperationException("Guild cache must be enabled");

            var restClient = channel.GetRestClient();
            return restClient.SendMessageAsync(guild, channel, message, options, cancellationToken);
        }
    }
}
