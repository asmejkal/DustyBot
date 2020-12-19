using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace DustyBot.Core.Logging
{
    public static class LoggerExtensions
    {
        private class ScopedLoggerAdapter : ILogger
        {
            private ILogger _inner;
            private Dictionary<string, object> _properties = new Dictionary<string, object>();

            public ScopedLoggerAdapter(ILogger inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public void AddProperty(string key, object value)
            {
                _properties.Add(key, value);
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return _inner.BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return _inner.IsEnabled(logLevel);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                using var scope = _inner.BeginScope(_properties);
                _inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }

        public static ILogger WithProperty(this ILogger logger, string key, object value)
        {
            if (logger is ScopedLoggerAdapter scoped)
            {
                scoped.AddProperty(key, value);
                return logger;
            }
            else
            {
                var result = new ScopedLoggerAdapter(logger);
                result.AddProperty(key, value);
                return result;
            }
        }

        public static IDisposable BeginScope(this ILogger logger, string key, object value)
        {
            return logger.BeginScope(new Dictionary<string, object> { { key, value } });
        }
    }
}
