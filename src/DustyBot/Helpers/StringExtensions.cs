using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
        }

        public static string TruncateLines(this string value, int maxLines)
        {
            var count = 0;
            for (int i = value.IndexOf('\n'); i >= 0 && i < value.Length; i = value.IndexOf('\n', i + 1))
            {
                count++;
                if (count == maxLines)
                    return value.Substring(0, i) + "...";
            }

            return value;
        }
    }
}
