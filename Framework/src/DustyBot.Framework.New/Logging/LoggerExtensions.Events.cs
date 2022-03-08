using Disqord.Bot.Hosting;
using Disqord.Gateway;
using DustyBot.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Logging
{
    public static partial class LoggerExtensions
    {
        public static LoggerScopeBuilder WithArgs(this ILogger logger, BanCreatedEventArgs e) =>
            logger.WithGuild(e.GuildId).WithUser(e.User);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, BanDeletedEventArgs e) =>
            logger.WithGuild(e.GuildId).WithUser(e.User);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, ChannelCreatedEventArgs e) =>
            logger.WithGuild(e.GuildId).WithChannel(e.Channel);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, ChannelDeletedEventArgs e) =>
            logger.WithGuild(e.GuildId).WithChannel(e.Channel);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, ChannelPinsUpdatedEventArgs e) => e switch
        {
            { GuildId: not null, Channel: not null } => logger.WithGuild(e.GuildId.Value).WithChannel(e.Channel),
            _ => logger.WithChannel(e.ChannelId)
        };

        public static LoggerScopeBuilder WithArgs(this ILogger logger, MemberJoinedEventArgs e) =>
            logger.WithGuild(e.GuildId).WithMember(e.Member);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, MemberLeftEventArgs e) =>
            logger.WithGuild(e.GuildId).WithUser(e.User);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, MessageDeletedEventArgs e) => e switch
        {
            { GuildId: not null } => logger.WithGuild(e.GuildId.Value).WithMessage(e.Message),
            _ => logger.WithMessage(e.Message)
        };

        public static LoggerScopeBuilder WithArgs(this ILogger logger, MessagesDeletedEventArgs e) =>
            logger.WithGuild(e.GuildId).WithChannel(e.ChannelId);

        public static LoggerScopeBuilder WithArgs(this ILogger logger, BotMessageReceivedEventArgs e) => e switch
        {
            { GuildId: not null, Channel: not null } => logger.WithGuild(e.GuildId.Value).WithChannel(e.Channel).WithMessage(e.Message),
            _ => logger.WithChannel(e.ChannelId).WithMessage(e.Message)
        };

        public static LoggerScopeBuilder WithArgs(this ILogger logger, TypingStartedEventArgs e) => e switch
        {
            { GuildId: not null, Member: not null } => logger.WithGuild(e.GuildId.Value).WithChannel(e.ChannelId).WithMember(e.Member),
            _ => logger.WithChannel(e.ChannelId)
        };
    }
}
