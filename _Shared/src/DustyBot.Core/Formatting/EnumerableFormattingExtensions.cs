using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DustyBot.Core.Formatting
{
    public static class EnumerableFormattingExtensions
    {
        public static string WordJoin(this IEnumerable<string> values, string separator = ", ", string lastSeparator = " and ")
        {
            string? previous = null;
            bool first = true;
            var result = new StringBuilder();
            foreach (var value in values)
            {
                if (!first && !string.IsNullOrEmpty(previous))
                {
                    if (result.Length > 0)
                        result.Append(separator);

                    result.Append(previous);
                }

                first = false;
                previous = value;
            }

            if (!string.IsNullOrEmpty(previous))
            {
                if (result.Length > 0)
                    result.Append(lastSeparator);

                result.Append(previous);
            }

            return result.ToString();
        }

        public static string WordJoin<T>(this IEnumerable<T> values, string separator = ", ", string lastSeparator = " and ") =>
            WordJoin(values.Select(x => x?.ToString() ?? throw new ArgumentNullException(nameof(values))), separator, lastSeparator);

        public static string WordJoinOr<T>(this IEnumerable<T> values, string separator = ", ", string lastSeparator = " or ") =>
            WordJoin(values, separator, lastSeparator);

        public static string WordJoinQuoted<T>(this IEnumerable<T> values, string quote = "`", string separator = ", ", string lastSeparator = " and ")
        {
            var result = new StringBuilder(values.WordJoin(quote + separator + quote, quote + lastSeparator + quote));
            return result.Length > 0 ? (quote + result + quote) : string.Empty;
        }

        public static string WordJoinQuotedOr<T>(this IEnumerable<T> values, string quote = "`", string separator = ", ", string lastSeparator = " or ") =>
            WordJoinQuoted(values, quote, separator, lastSeparator);
    }
}
