using System;
using DustyBot.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Logging
{
    public static class LoggerExtensions
    {
        public static IDisposable BuildScope(this ILogger logger, Action<LoggerScopeBuilder> configure)
        {
            var builder = logger.BuildScope();
            configure(builder);

            return builder.Begin();
        }

        public static ScopedLoggerAdapter WithScope(this ILogger logger, Action<LoggerScopeBuilder> configure)
        {
            var builder = logger.BuildScope();
            configure(builder);

            return logger.WithScope(builder.CorrelationIds);
        }

        public static LoggerScopeBuilder BuildScope(this ILogger logger) =>
            new LoggerScopeBuilder(logger);
    }
}