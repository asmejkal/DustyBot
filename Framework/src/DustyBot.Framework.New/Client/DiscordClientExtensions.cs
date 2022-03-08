using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Exceptions;

namespace DustyBot.Framework.Client
{
    public static class DiscordClientExtensions
    {
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

        public static Task<IUserMessage> SendMessageCheckedAsync(this DiscordClientBase client,
            Snowflake guildId,
            Snowflake channelId,
            LocalMessage message,
            IRestRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!client.CacheProvider.TryGetGuilds(out var guilds) || !guilds.TryGetValue(guildId, out var guild))
                throw new InvalidOperationException("Guild cache must be enabled");

            if (!client.CacheProvider.TryGetChannels(guildId, out var channels) || !channels.TryGetValue(channelId, out var channel))
                throw new InvalidOperationException("Channel cache must be enabled");

            if (channel is not IMessageGuildChannel messageChannel)
                throw new ArgumentException("Channel is not a message channel", nameof(channelId));

            return SendMessageCheckedAsync(client, guild, messageChannel, message, options, cancellationToken);
        }
    }
}
