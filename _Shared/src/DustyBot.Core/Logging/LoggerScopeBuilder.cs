using System;
using System.Collections.Generic;
using DustyBot.Core.Disposal;
using Microsoft.Extensions.Logging;

namespace DustyBot.Core.Logging
{
    public class LoggerScopeBuilder : ILogger
    {
        public ILogger Logger { get; }
        public IReadOnlyDictionary<string, object?> CorrelationIds => _correlationIds;

        private readonly Dictionary<string, object?> _correlationIds = new();

        public LoggerScopeBuilder(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IDisposable BeginScope() => Logger.BeginScope(_correlationIds);

        public LoggerScopeBuilder With(string key, object? value)
        {
            _correlationIds[key] = value;
            return this;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            using var scope = BeginScope();
            Logger.Log(logLevel, eventId, state, exception, formatter);
        }

        public bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

        public IDisposable BeginScope<TState>(TState state) =>
            new DisposableWrapper(BeginScope(), Logger.BeginScope(state));
    }
}
