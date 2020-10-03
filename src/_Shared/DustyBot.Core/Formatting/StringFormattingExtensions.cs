using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DustyBot.Core.Formatting
{
    public static class StringFormattingExtensions
    {
        public static bool TryAppendLimited(this StringBuilder o, string value, int maxLength)
        {
            if (o.Length + value.Length <= maxLength)
            {
                o.Append(value);
                return true;
            }

            return false;
        }

        public static bool TryAppendLineLimited(this StringBuilder o, string value, int maxLength)
            => o.TryAppendLimited(value + Environment.NewLine, maxLength);

        public static string Truncate(this string value, int maxChars, string ellipsis = "...")
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars - ellipsis.Length) + ellipsis;
        }

        public static string TruncateLines(this string value, int maxLines, string ellipsis = "...", bool trim = false)
        {
            var count = 0;
            for (int i = value.IndexOf('\n'); i >= 0 && i < value.Length; i = value.IndexOf('\n', i + 1))
            {
                count++;
                if (count == maxLines)
                {
                    if (trim)
                        return value.Substring(0, i).TrimEnd() + ellipsis;
                    else
                        return value.Substring(0, i) + ellipsis;
                }
            }

            return value;
        }

        public static string JoinWhiteLines(this string value, int maxConsecutive = 1)
        {
            using (StringReader reader = new StringReader(value))
            {
                StringBuilder result = new StringBuilder();
                int whiteLineCount = 0;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        whiteLineCount++;
                        if (whiteLineCount <= maxConsecutive)
                            result.AppendLine(line);
                    }
                    else
                    {
                        whiteLineCount = 0;
                        result.AppendLine(line);
                    }
                }

                return result.ToString();
            }
        }

        public static string SplitCamelCase(this string value, string delimiter = " ")
        {
            return string.Join(delimiter, Regex.Matches(value, @"(^\p{Ll}+|\p{Lu}+(?!\p{Ll})|\p{Lu}\p{Ll}+)")
                .OfType<Match>()
                .Select(m => m.Value));
        }
    }
}
