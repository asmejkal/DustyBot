using System.Collections.Generic;
using Microsoft.Extensions.Logging;

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
