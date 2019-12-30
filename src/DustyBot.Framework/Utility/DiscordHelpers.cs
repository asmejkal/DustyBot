using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Linq;
using System.Text.RegularExpressions;
using Discord.WebSocket;

namespace DustyBot.Framework.Utility
{
    public static class DiscordHelpers
    {
        public const int MaxEmbedFieldLength = 1024;

        private static readonly Regex MarkdownUriRegex = new Regex(@"^\[(.+)\]\((.+)\)$", RegexOptions.Compiled);
        private static readonly Regex MarkdownQuoteRegex = new Regex(@"^> ", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly IEnumerable<char> MarkdownCharacters = new[] { '*', '_', '~', '`', '[', ']', '(', ')' };

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

        public static string BuildMarkdownUri(string text, Uri uri) => BuildMarkdownUri(text, uri.AbsoluteUri);

        public static string BuildMarkdownUri(string text, string uri)
            => $"[{text}]({uri.SanitiseMarkdownUri()})";

        public static string SanitiseMarkdownUri(this string uri) => uri?.Replace(")", "%29");

        public static Uri SanitiseMarkdownUri(this Uri uri) => new Uri(uri.AbsoluteUri.SanitiseMarkdownUri());

        public static string EscapeMarkdown(this string text)
        {
            var result = new StringBuilder(text);
            foreach (var c in text)
            {
                if (MarkdownCharacters.Contains(c))
                    result.Append('\\');

                result.Append(c);
            }

            return MarkdownQuoteRegex.Replace(result.ToString(), @"\> ");
        }

        public static (string Text, Uri Uri)? TryParseMarkdownUri(string text)
        {
            var match = MarkdownUriRegex.Match(text);
            if (match.Success && Uri.TryCreate(match.Groups[2].Value, UriKind.Absolute, out var uri))
                return (match.Groups[1].Value, uri);

            return null;
        }

        public static string TrimLinkBraces(string link) => link.Trim('<', '>');
    }
}
