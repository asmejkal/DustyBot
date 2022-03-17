﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Client;

namespace DustyBot.Framework.Entities
{
    public static class GuildExtensions
    {
        public static ChannelPermissions GetBotPermissions(this IGatewayGuild guild, IGuildChannel channel) =>
            guild.GetPermissions(channel, guild.GetMember(guild.Client.CurrentUser.Id));

        public static ChannelPermissions GetPermissions(this IGatewayGuild guild, IGuildChannel channel, IMember member)
        {
            var roles = member.GetRoles();
            return Discord.Permissions.CalculatePermissions(guild, channel, member, roles.Values);
        }

        public static async Task<ChannelPermissions?> FetchBotPermissionsAsync(
            this IGatewayGuild guild, 
            IGuildChannel channel,
            IRestRequestOptions? options = null, 
            CancellationToken cancellationToken = default)
        {
            var member = await guild.FetchMemberAsync(guild.Client.CurrentUser.Id, options, cancellationToken);
            var roles = member.GetRoles();
            return Discord.Permissions.CalculatePermissions(guild, channel, member, roles.Values);
        }

        public static CachedGuildChannel GetChannelOrThrow(this IGatewayGuild guild, Snowflake channelId) =>
            guild.Client.GetChannelOrThrow(guild.Id, channelId);

        public static CachedRole GetRole(this IGatewayGuild guild, Snowflake roleId) =>
            guild.Client.GetRole(guild.Id, roleId);

        public static IReadOnlyDictionary<Snowflake, CachedRole> GetRoles(this IGatewayGuild guild) =>
            guild.Client.GetRoles(guild.Id);

        public static CachedRole GetEveryoneRole(this IGatewayGuild guild) =>
            guild.Client.GetRole(guild.Id, guild.Id);

        public static Snowflake GetEveryoneRoleId(this IGuild guild) => guild.Id;

        public static async Task<IMember> GetOrFetchMemberAsync(
            this IGatewayGuild guild, 
            Snowflake memberId, 
            IRestRequestOptions? options = null, 
            CancellationToken cancellationToken = default)
        {
            return guild.GetMember(memberId) ?? await guild.FetchMemberAsync(memberId, options, cancellationToken);
        }

        public static IEnumerable<KeyValuePair<Snowflake, CachedGuildChannel>> GetChannels(this IGatewayGuild guild, ChannelType type) => 
            guild.GetChannels().Where(x => x.Value.Type == type);

        public static IEnumerable<CachedTextChannel> GetTextChannels(this IGatewayGuild guild) =>
            guild.GetChannels(ChannelType.Text).Select(x => x.Value).OfType<CachedTextChannel>();
    }
}