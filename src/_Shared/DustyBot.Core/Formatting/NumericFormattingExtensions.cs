using System;
using System.Collections.Generic;

namespace DustyBot.Core.Formatting
{
    public static class NumericFormattingExtensions
    {
        private static readonly IReadOnlyList<string> Keycaps =
            new List<string>() { "0️⃣", "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣" };

        public static string ToEnglishOrdinal(this int num)
        {
            if (num <= 0) 
                return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }

        public static string ToKeycapEmoji(this int num)
        {
            if (num < 0 || num > 9)
                throw new ArgumentOutOfRangeException("Only values within 0-9 are allowed.");

            return Keycaps[num];
        }

        public static string ToStringBigNumber(this int num)
        {
            double numStr;
            string suffix;
            if (num < 1000d)
            {
                numStr = num;
                suffix = "";
            }
            else if (num < 1000000d)
            {
                numStr = num / 1000d;
                suffix = "K";
            }
            else if (num < 1000000000d)
            {
                numStr = num / 1000000d;
                suffix = "M";
            }
            else
            {
                numStr = num / 1000000000d;
                suffix = "B";
            }

            return numStr.ToString("#.##") + suffix;
        }
    }
}
