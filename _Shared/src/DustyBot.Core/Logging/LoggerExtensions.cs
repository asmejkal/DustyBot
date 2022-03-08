using Microsoft.Extensions.Logging;

namespace DustyBot.Core.Logging
{
    public static class LoggerExtensions
    {
        public static LoggerScopeBuilder With(this ILogger logger, string key, object? value) => logger switch
        {
            LoggerScopeBuilder builder => builder.With(key, value),
            _ => new LoggerScopeBuilder(logger).With(key, value)
        };
    }
}
