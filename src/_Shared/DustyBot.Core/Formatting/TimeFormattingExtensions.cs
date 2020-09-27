using System;

namespace DustyBot.Core.Formatting
{
    public static class TimeFormattingExtensions
    {
        public static string SimpleFormat(this TimeSpan value)
        {
            bool negative = value < TimeSpan.Zero;
            if (negative)
                value = value.Negate();

            string result;
            if (value < TimeSpan.FromSeconds(1))
                return "now";

            if (value < TimeSpan.FromMinutes(1))
                result = $"{value.Seconds}s";
            else if (value < TimeSpan.FromHours(1))
                result = $"{value.Minutes}min";
            else if (value < TimeSpan.FromDays(1))
                result = $"{value.Hours}h";
            else if (value < TimeSpan.FromDays(7))
                result = $"{value.Days}d";
            else
                result = $"{value.Days / 7}w";

            if (negative)
                result += " ago";

            return result;
        }
    }
}
