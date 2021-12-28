using System;
using System.Text;

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
                result = $"{value.Milliseconds}ms";
            else if (value < TimeSpan.FromMinutes(1))
                result = $"{value.Seconds}s";
            else if (value < TimeSpan.FromHours(1))
                result = $"{value.Minutes}min";
            else if (value < TimeSpan.FromDays(1))
                result = $"{value.Hours}h";
            else if (value < TimeSpan.FromDays(7))
                result = $"{value.Days}d {value.Hours}h";
            else if (value < TimeSpan.FromDays(365))
                result = $"{value.Days}d";
            else
                result = $"{value.Days / 365}y {value.Days % 365}d";

            if (negative)
                result += " ago";

            return result;
        }

        public static string SimpleFormatPrecise(this TimeSpan value)
        {
            bool negative = value < TimeSpan.Zero;
            if (negative)
                value = value.Negate();

            var result = new StringBuilder();
            if (value >= TimeSpan.FromDays(365))
                result.Append($"{value.Days / 365}y ");

            if (value >= TimeSpan.FromDays(1))
                result.Append($"{value.Days}d ");

            if (value >= TimeSpan.FromHours(1))
                result.Append($"{value.Hours}h ");

            if (value >= TimeSpan.FromMinutes(1))
                result.Append($"{value.Minutes}min ");

            if (value >= TimeSpan.FromSeconds(1))
                result.Append($"{value.Seconds}s ");
            else
                result.Append($"{value.Milliseconds}ms ");
            
            if (negative)
                result.Append("ago ");

            return result.ToString(0, result.Length - 1);
        }
    }
}
