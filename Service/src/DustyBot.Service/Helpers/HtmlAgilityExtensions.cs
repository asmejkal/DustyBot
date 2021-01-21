using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace DustyBot.Service.Helpers
{
    internal static class HtmlAgilityExtensions
    {
        // https://stackoverflow.com/a/30088920
        public static string ToPlainText(this HtmlAgilityPack.HtmlDocument doc) => doc.DocumentNode.ToPlainText();

        public static string ToPlainText(this HtmlAgilityPack.HtmlNode node)
        {
            var builder = new System.Text.StringBuilder();
            var state = ToPlainTextState.StartLine;

            Plain(builder, ref state, new[] { node });
            
            return builder.ToString();
        }

        // System.Xml.Linq part
        public static string ToPlainText(this IEnumerable<XNode> nodes)
        {
            var builder = new System.Text.StringBuilder();
            var state = ToPlainTextState.StartLine;

            Plain(builder, ref state, nodes);
            return builder.ToString();
        }

        public static void Process(StringBuilder builder, ref ToPlainTextState state, params char[] chars)
        {
            foreach (var ch in chars)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (IsHardSpace(ch))
                    {
                        if (state == ToPlainTextState.WhiteSpace)
                            builder.Append(' ');
                        builder.Append(' ');
                        state = ToPlainTextState.NotWhiteSpace;
                    }
                    else
                    {
                        if (state == ToPlainTextState.NotWhiteSpace)
                            state = ToPlainTextState.WhiteSpace;
                    }
                }
                else
                {
                    if (state == ToPlainTextState.WhiteSpace)
                        builder.Append(' ');
                    builder.Append(ch);
                    state = ToPlainTextState.NotWhiteSpace;
                }
            }
        }

        private static void Plain(StringBuilder builder, ref ToPlainTextState state, IEnumerable<HtmlAgilityPack.HtmlNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is HtmlAgilityPack.HtmlTextNode)
                {
                    var text = (HtmlAgilityPack.HtmlTextNode)node;
                    Process(builder, ref state, HtmlAgilityPack.HtmlEntity.DeEntitize(text.Text).ToCharArray());
                }
                else
                {
                    var tag = node.Name.ToLower();

                    if (tag == "br")
                    {
                        builder.AppendLine();
                        state = ToPlainTextState.StartLine;
                    }
                    else if (NonVisibleTags.Contains(tag))
                    {
                    }
                    else if (InlineTags.Contains(tag))
                    {
                        Plain(builder, ref state, node.ChildNodes);
                    }
                    else
                    {
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }

                        Plain(builder, ref state, node.ChildNodes);
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }
                    }
                }
            }
        }

        private static void Plain(StringBuilder builder, ref ToPlainTextState state, IEnumerable<XNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is XElement)
                {
                    var element = (XElement)node;
                    var tag = element.Name.LocalName.ToLower();

                    if (tag == "br")
                    {
                        builder.AppendLine();
                        state = ToPlainTextState.StartLine;
                    }
                    else if (NonVisibleTags.Contains(tag))
                    {
                    }
                    else if (InlineTags.Contains(tag))
                    {
                        Plain(builder, ref state, element.Nodes());
                    }
                    else
                    {
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }

                        Plain(builder, ref state, element.Nodes());
                        if (state != ToPlainTextState.StartLine)
                        {
                            builder.AppendLine();
                            state = ToPlainTextState.StartLine;
                        }
                    }
                }
                else if (node is XText)
                {
                    var text = (XText)node;
                    Process(builder, ref state, text.Value.ToCharArray());
                }
            }
        }

        private static bool IsHardSpace(char ch)
        {
            return ch == 0xA0 || ch == 0x2007 || ch == 0x202F;
        }

        private static readonly HashSet<string> InlineTags = new HashSet<string>
        {
            // from https://developer.mozilla.org/en-US/docs/Web/HTML/Inline_elemente
            "b", "big", "i", "small", "tt", "abbr", "acronym",
            "cite", "code", "dfn", "em", "kbd", "strong", "samp",
            "var", "a", "bdo", "br", "img", "map", "object", "q",
            "script", "span", "sub", "sup", "button", "input", "label",
            "select", "textarea"
        };

        private static readonly HashSet<string> NonVisibleTags = new HashSet<string>
        {
            "script", "style"
        };

        public enum ToPlainTextState
        {
            StartLine = 0,
            NotWhiteSpace,
            WhiteSpace,
        }
    }
}
