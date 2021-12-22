using System;

namespace DustyBot.Framework.Utility
{
    public static class UriExtensions
    {
        public static string ToMarkdown(this Uri uri, string title) => Disqord.Markdown.Link(title, uri.AbsoluteUri.Replace(")", "%29"));
    }
}
