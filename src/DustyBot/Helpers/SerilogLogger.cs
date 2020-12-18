using Discord;
using DustyBot.Framework.Logging;
using System;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    internal class SerilogLogger : ILogger
    {
        private Serilog.ILogger _logger;

        public SerilogLogger(Serilog.ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task Log(LogMessage message)
        {
            _logger.Write(message);
            return Task.CompletedTask;
        }
    }
}
