using System.Collections.Generic;
using System.Text;

namespace DustyBot.Core.Formatting
{
    public static class EnumerableFormattingExtensions
    {
        public static string WordJoin<T>(this IEnumerable<T> values, string separator = ", ", string lastSeparator = ", and ")
        {
            var it = values.GetEnumerator();
            bool isFirst = true;
            bool hasRemainingItems;
            T item = default;
            string result = null;

            do
            {
                hasRemainingItems = it.MoveNext();
                if (hasRemainingItems)
                {
                    if (!isFirst)
                    {
                        if (result == null)
                            result = item.ToString();
                        else
                            result += separator + item.ToString();
                    }

                    item = it.Current;
                    isFirst = false;
                }
            } while (hasRemainingItems);

            if (result == null)
                result = item != null ? item.ToString() : string.Empty;
            else
                result += lastSeparator + item.ToString();

            return result;
        }

        public static string WordJoinOr<T>(this IEnumerable<T> values, string separator = ", ", string lastSeparator = ", or ") =>
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
