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
        public static Task<IUserMessage> SendMessageCheckedAsync(
            this IMessageGuildChannel channel,
            LocalMessage message,
            IRestRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return ((DiscordClientBase)channel.Client).SendMessageCheckedAsync(channel.GuildId, channel.Id, message, options, cancellationToken);
        }
    }
}
