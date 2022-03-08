using System;
using Disqord.Bot;
using DustyBot.Core.Logging;
using DustyBot.Framework.Commands;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Logging
{
    public static partial class LoggerExtensions
    {
        public static LoggerScopeBuilder WithCorrelationId(this ILogger logger, Guid correlationId) =>
            logger.With(LogFields.CorrelationId, correlationId);

        public static LoggerScopeBuilder WithCommandContext(this ILogger logger, DiscordCommandContext x)
        {
            var scope = x switch
            {
                DustyGuildCommandContext y => logger.WithCorrelationId(y.CorrelationId).WithGuild(y.Guild).WithMember(y.Author),
                DustyCommandContext y => logger.WithCorrelationId(y.CorrelationId),
                _ => logger
            };

            return scope.With(LogFields.Command, x.Command.FullAliases[0]).WithMessage(x.Message);
        }

        public static LoggerScopeBuilder WithCommandUsageContext(this ILogger logger, DiscordCommandContext x) =>
            logger.With(LogFields.Prefix, x.Prefix).With(LogFields.CommandAlias, x.Path);
    }
}
