﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;

namespace DustyBot.Framework.Client
{
    public static class DiscordClientExtensions
    {
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

            return client.SendMessageCheckedAsync(guild, messageChannel, message, options, cancellationToken);
        }
    }
}