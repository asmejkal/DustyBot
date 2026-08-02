using System;
using System.Linq;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Text;
using Disqord.Gateway;
using DustyBot.Core.Logging;
using DustyBot.Framework.Commands;
using Microsoft.Extensions.Logging;
using Qmmands.Text;
using Qommon.Metadata;

namespace DustyBot.Framework.Logging
{
    public static partial class LoggerExtensions
    {
        public static LoggerScopeBuilder WithCorrelationId(this ILogger logger, Guid correlationId) =>
            logger.With(LogFields.CorrelationId, correlationId);

        public static LoggerScopeBuilder WithCommandContext(this ILogger logger, IDiscordCommandContext x)
        {
            var scope = logger.GetScopeBuilder();
            if (x.TryGetMetadata<Guid>(MetadataKeys.CorrelationId, out var correlationId))
                scope.WithCorrelationId(correlationId);

            if (x is IDiscordGuildCommandContext guildCommandContext)
            {
                var guild = x.Bot.GetGuild(guildCommandContext.GuildId);
                if (guild is not null)
                    scope.WithGuild(guild);
                else
                    scope.WithGuild(guildCommandContext.GuildId);

                scope.WithMember(guildCommandContext.Author);
            }

            if (x is IDiscordTextCommandContext textCommandContext)
            {
                if (textCommandContext.Command is not null)
                    scope.With(LogFields.Command, textCommandContext.Command?.EnumerateFullAliases().First());

                scope.WithMessage(textCommandContext.Message);
            }

            return scope;
        }

        public static LoggerScopeBuilder WithCommandUsageContext(this ILogger logger, IDiscordTextCommandContext x) =>
            logger.With(LogFields.Prefix, x.Prefix).With(LogFields.CommandAlias, x.Path);
    }
}
