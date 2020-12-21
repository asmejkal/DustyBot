using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DustyBot.Core.Logging
{
    public static class LoggerExtensions
    {
        public static ILogger WithScope(this ILogger logger, string key, object value)
        {
            return new ScopedLoggerAdapter(logger, new[] { KeyValuePair.Create(key, value) });
        }

        public static ILogger WithScope(this ILogger logger, IEnumerable<KeyValuePair<string, object>> fields)
        {
            return new ScopedLoggerAdapter(logger, fields);
        }
    }
}
