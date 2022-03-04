﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DustyBot.Core.Disposal;
using Microsoft.Extensions.Logging;

namespace DustyBot.Core.Logging
{
    public class ScopedLoggerAdapter : ILogger
    {
        private readonly ILogger _inner;
        private readonly ImmutableDictionary<string, object> _fields;

        public ScopedLoggerAdapter(ILogger inner, IEnumerable<KeyValuePair<string, object>> fields)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _fields = fields.ToImmutableDictionary();
        }

        public ScopedLoggerAdapter(ILogger inner, string key, object value)
            : this(inner, new[] { KeyValuePair.Create(key, value) })
        {
        }

        public IDisposable BeginScope() => _inner.BeginScope(_fields);

        public IDisposable BeginScope<TState>(TState state) => new DisposableWrapper(BeginScope(), _inner.BeginScope(state));

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            using var scope = _inner.BeginScope(_fields);
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
