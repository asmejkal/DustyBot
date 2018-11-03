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

        private static Regex UserMentionRegex = new Regex("<@!?([0-9]+)>", RegexOptions.Compiled);
        public static async Task<string> ReplaceUserMentions(string content, IEnumerable<ulong> mentionedUserIds, IGuild guild)
        {
            var names = new Dictionary<ulong, string>();
            foreach (var userId in mentionedUserIds)
            {
                var user = await guild.GetUserAsync(userId);
                if (user != null)
                    names[userId] = user.Username;
            }

            return UserMentionRegex.Replace(content, x =>
            {
                if (string.IsNullOrEmpty(x.Groups[1].Value))
                    return x.Value;

                if (!ulong.TryParse(x.Groups[1].Value, out var id) || !names.TryGetValue(id, out var name))
                    return x.Value;

                return "@" + name;
            });
        }

        private static Regex RoleMentionRegex = new Regex("<@&?([0-9]+)>", RegexOptions.Compiled);
        public static string ReplaceRoleMentions(string content, IEnumerable<ulong> mentionedRoleIds, IGuild guild)
        {
            var names = new Dictionary<ulong, string>();
            foreach (var roleId in mentionedRoleIds)
            {
                var role = guild.GetRole(roleId);
                if (role != null)
                    names[roleId] = role.Name;
            }

            return RoleMentionRegex.Replace(content, x =>
            {
                if (string.IsNullOrEmpty(x.Groups[1].Value))
                    return x.Value;

                if (!ulong.TryParse(x.Groups[1].Value, out var id) || !names.TryGetValue(id, out var name))
                    return x.Value;

                return "@" + name;
            });
        }

        public static async Task<string> ReplaceMentions(string content, IEnumerable<ulong> mentionedUserIds, IEnumerable<ulong> mentionedRoleIds, IGuild guild)
        {
            var result = await ReplaceUserMentions(content, mentionedUserIds, guild);
            return ReplaceRoleMentions(result, mentionedRoleIds, guild);
        }

        public static string EscapeMentions(string mention) => mention.Replace("@", "@\u200B");

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
                    try
                    {
                        var message = await textChannel.GetMessageAsync(id);
                        if (message != null)
                            return message;
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                    {
                        //Missing access
                    }
                }
            }

            return null;
        }

        public static string GetFullName(this IEmote emote)
        {
            if (emote is Emote customEmote)
                return $"<:{customEmote.Name}:{customEmote.Id}>";
            else
                return emote.Name;
        }
    }
}
