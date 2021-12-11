using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Logging
{
    public sealed partial class LoggerScopeBuilder
    {
        public ILogger Logger { get; }
        public IReadOnlyDictionary<string, object> CorrelationIds => _correlationIds;

        private readonly Dictionary<string, object> _correlationIds = new();

        public LoggerScopeBuilder(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IDisposable Begin() => Logger.BeginScope(_correlationIds);

        public LoggerScopeBuilder With(string key, object value)
        {
            _correlationIds[key] = value;
            return this;
        }

        public LoggerScopeBuilder With(IEnumerable<KeyValuePair<string, object>> fields)
        {
            foreach (var (key, value) in fields)
                _correlationIds[key] = value;

            return this;
        }
    }
}
