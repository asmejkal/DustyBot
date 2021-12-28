using System;
using Disqord.Bot;
using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Logging
{
    public sealed partial class LoggerScopeBuilder
    {
        public LoggerScopeBuilder WithCorrelationId(Guid correlationId) =>
            With(LogFields.CorrelationId, correlationId);

        public LoggerScopeBuilder WithCommandContext(DiscordCommandContext x)
        {
            var scope = x switch
            {
                DustyGuildCommandContext y => WithCorrelationId(y.CorrelationId).WithGuild(y.Guild).WithMember(y.Author),
                DustyCommandContext y => WithCorrelationId(y.CorrelationId),
                _ => this
            };

            return scope.With(LogFields.Command, x.Command.FullAliases[0]).WithMessage(x.Message);
        }

        public LoggerScopeBuilder WithCommandUsageContext(DiscordCommandContext x) =>
            With(LogFields.Prefix, x.Prefix).With(LogFields.CommandAlias, x.Path);
    }
}
