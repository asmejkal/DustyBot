using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Exceptions;

namespace DustyBot.Framework.Client
{
    public static class RestClientExtensions
    {
        public static IDisposable BeginTyping(
            this IRestClient client,
            Snowflake channelId,
            TimeSpan timeout,
            IRestRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return new LimitedTypingRepeater(client, channelId, timeout, options, cancellationToken);
        }

        public static Task<IUserMessage> SendMessageCheckedAsync(this IRestClient client,
            IGatewayGuild guild,
            IMessageGuildChannel channel,
            LocalMessage message,
            IRestRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var permissions = guild.GetBotPermissions(channel);
            if (message.Embeds != null && message.Embeds.Count > 0 && !permissions.SendEmbeds)
                throw new MissingPermissionsException($"Bot is missing permissions to send embeds in channel {channel.Id} on guild {guild.Id}");

            if (!permissions.SendMessages)
                throw new MissingPermissionsException($"Bot is missing permissions to send messages in channel {channel.Id} on guild {guild.Id}");

            return client.SendMessageAsync(channel.Id, message, options, cancellationToken);
        }
    }
}
