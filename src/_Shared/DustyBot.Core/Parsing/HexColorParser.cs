using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DustyBot.Core.Parsing
{
    public static class HexColorParser
    {
        private static Regex ColorCodeRegex = new Regex("^#?([a-fA-F0-9]+)$", RegexOptions.Compiled);

        public static bool TryParse(string value, out uint result)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var match = ColorCodeRegex.Match(value);
            if (match.Success)
            {
                var number = match.Groups[1].Value;
                return uint.TryParse(number, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
            }

            result = 0;
            return false;
        }

        public static uint Parse(string value)
        {
            if (!TryParse(value, out var result))
                throw new ArgumentException("Failed to parse value");

            return result;
        }
    }
}
