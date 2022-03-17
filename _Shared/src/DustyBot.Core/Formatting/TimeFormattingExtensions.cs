using System;
using System.Text;

namespace DustyBot.Core.Formatting
{
    public static class TimeFormattingExtensions
    {
        public static string SimpleFormat(this TimeSpan value, TimeSpanPrecision precision = TimeSpanPrecision.Low)
        {
            bool negative = value < TimeSpan.Zero;
            if (negative)
                value = value.Negate();

            var result = new StringBuilder();
            if (precision == TimeSpanPrecision.High)
            {
                if (value >= TimeSpan.FromDays(365))
                    result.Append($"{value.Days / 365}y ");

                if (value >= TimeSpan.FromDays(1))
                    result.Append($"{value.Days % 365}d ");

                if (value >= TimeSpan.FromHours(1))
                    result.Append($"{value.Hours}h ");

                if (value >= TimeSpan.FromMinutes(1))
                    result.Append($"{value.Minutes}min ");

                if (value >= TimeSpan.FromSeconds(1))
                    result.Append($"{value.Seconds}s ");
                else
                    result.Append($"{value.Milliseconds}ms ");
            }
            else if (precision == TimeSpanPrecision.Medium)
            {
                if (value < TimeSpan.FromSeconds(1))
                    result.Append($"{value.Milliseconds}ms ");
                else if (value < TimeSpan.FromMinutes(1))
                    result.Append($"{value.Seconds}s ");
                else if (value < TimeSpan.FromMinutes(10))
                    result.Append($"{value.Minutes}min {value.Seconds}s ");
                else if (value < TimeSpan.FromHours(1))
                    result.Append($"{value.Minutes}min ");
                else if (value < TimeSpan.FromDays(1))
                    result.Append($"{value.Hours}h {value.Minutes}min ");
                else if (value < TimeSpan.FromDays(7))
                    result.Append($"{value.Days}d {value.Hours}h ");
                else if (value < TimeSpan.FromDays(365))
                    result.Append($"{value.Days}d ");
                else
                    result.Append($"{value.Days / 365}y {value.Days % 365}d");
            }
            else if (precision == TimeSpanPrecision.Low)
            {
                if (value < TimeSpan.FromSeconds(1))
                    result.Append($"{value.Milliseconds}ms ");
                else if (value < TimeSpan.FromMinutes(1))
                    result.Append($"{value.Seconds}s ");
                else if (value < TimeSpan.FromHours(1))
                    result.Append($"{value.Minutes}min ");
                else if (value < TimeSpan.FromDays(1))
                    result.Append($"{value.Hours}h ");
                else if (value < TimeSpan.FromDays(7))
                    result.Append($"{value.Days}d {value.Hours}h ");
                else if (value < TimeSpan.FromDays(365))
                    result.Append($"{value.Days}d ");
                else
                    result.Append($"{value.Days / 365}y {value.Days % 365}d");
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(precision));
            }

            if (negative)
                result.Append("ago ");

            return result.ToString(0, result.Length - 1);
        }
    }
}
