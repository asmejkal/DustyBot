using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Helpers
{
    static class TimeHelpers
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

        public static DateTimeOffset UnixTimeStampToDateTimeOffset(double unixTimeStamp)
        {
            var dt = new DateTimeOffset(1970, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);
            return dt.AddSeconds(unixTimeStamp);
        }
    }
}
