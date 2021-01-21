using System;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Serilog;
using Serilog.Events;

namespace DustyBot.Service.Helpers
{
    internal static class SerilogExtensions
    {
        public static T UseSerilog<T>(this T client, ILogger logger)
            where T : BaseDiscordClient
        {
            client.Log += x =>
            {
                logger.Write(x);
                return Task.CompletedTask;
            };

            return client;
        }

        public static void Write(this ILogger logger, LogMessage discordLog)
        {
            logger.ForContext("Source", discordLog.Source)
                .Write(discordLog.Severity.ToSerilog(), discordLog.Exception, discordLog.Message);
        }

        public static LogEventLevel ToSerilog(this LogSeverity severity) =>
            severity switch
            {
                LogSeverity.Debug => LogEventLevel.Debug,
                LogSeverity.Verbose => LogEventLevel.Verbose,
                LogSeverity.Info => LogEventLevel.Information,
                LogSeverity.Warning => LogEventLevel.Warning,
                LogSeverity.Error => LogEventLevel.Error,
                LogSeverity.Critical => LogEventLevel.Fatal,
                _ => throw new ArgumentException($"Unknown enum value {severity}", paramName: nameof(severity))
            };
    }
}
