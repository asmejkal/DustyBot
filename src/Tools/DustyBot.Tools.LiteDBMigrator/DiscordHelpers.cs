using System;
using System.Text.RegularExpressions;

namespace DustyBot.Framework.Utility
{
    public static class DiscordHelpers
    {
        private static readonly Regex MarkdownUriRegex = new Regex(@"^\[(.+)\]\((.+)\)$", RegexOptions.Compiled);

        public static (string Text, Uri Uri)? TryParseMarkdownUri(string text)
        {
            var match = MarkdownUriRegex.Match(text);
            if (match.Success && Uri.TryCreate(match.Groups[2].Value, UriKind.Absolute, out var uri))
                return (match.Groups[1].Value, uri);

            return null;
        }
    }
}
