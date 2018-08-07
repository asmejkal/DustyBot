using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Linq;
using System.Text.RegularExpressions;

namespace DustyBot.Framework.Utility
{
    public static class DiscordHelpers
    {
        public static async Task EnsureBotPermissions(IGuild guild, params GuildPermission[] perms)
        {
            var user = await guild.GetCurrentUserAsync();
            var missing = new HashSet<GuildPermission>();
            foreach (var perm in perms)
            {
                if (!user.GuildPermissions.Has(perm))
                    missing.Add(perm);
            }

            if (missing.Count > 0)
                throw new Exceptions.MissingBotPermissionsException(missing.ToArray());
        }

        public static async Task EnsureBotPermissions(IGuildChannel channel, params ChannelPermission[] perms)
        {
            var user = await channel.Guild.GetCurrentUserAsync();
            var missing = new HashSet<ChannelPermission>();
            foreach (var perm in perms)
            {
                if (!user.GetPermissions(channel).Has(perm))
                    missing.Add(perm);
            }

            if (missing.Count > 0)
                throw new Exceptions.MissingBotChannelPermissionsException(missing.ToArray());
        }

        public static async Task EnsureBotPermissions(IChannel channel, params ChannelPermission[] perms)
        {
            var guildChannel = channel as IGuildChannel;
            await EnsureBotPermissions(guildChannel, perms);
        }

        public static string EscapeMention(string mention) => mention.Replace("@", "@\u200B");
        
        public static string Sanitise(this string value)
        {
            return value
                .Replace("@everyone", "@\u200Beveryone")
                .Replace("@here", "@\u200Bhere");
        }

        public static async Task<IMessage> GetMessageAsync(this IGuild guild, ulong id)
        {
            foreach (var c in await guild.GetChannelsAsync())
            {
                if (c is ITextChannel textChannel)
                {
                    var message = await textChannel.GetMessageAsync(id);
                    if (message != null)
                        return message;
                }
            }

            return null;
        }
    }
}
