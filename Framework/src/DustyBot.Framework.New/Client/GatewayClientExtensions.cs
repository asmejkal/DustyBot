using System;
using Disqord;
using Disqord.Gateway;

namespace DustyBot.Framework.Client
{
    public static class GatewayClientExtensions
    {
        public static CachedGuild GetGuildOrThrow(this IGatewayClient client, Snowflake guildId) =>
            client.GetGuild(guildId) ?? throw new InvalidOperationException($"Guild {guildId} is missing in cache");

        public static CachedGuildChannel GetChannelOrThrow(this IGatewayClient client, Snowflake guildId, Snowflake channelId) =>
            client.GetChannel(guildId, channelId) ?? throw new InvalidOperationException($"Channel {channelId} is not cached for guild {guildId}");
    }
}
