using Disqord;
using DustyBot.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Logging
{
    public static partial class LoggerExtensions
    {
        public static LoggerScopeBuilder WithMessage(this ILogger logger, IMessage message) =>
            logger.WithMessage(message.Id).WithUser(message.Author).WithChannel(message.ChannelId);

        public static LoggerScopeBuilder WithMessage(this ILogger logger, ulong messageId) =>
            logger.With(LogFields.MessageId, messageId);

        public static LoggerScopeBuilder WithChannel(this ILogger logger, IChannel channel) =>
            logger.WithChannel(channel.Id).WithChannelName(channel.Name);

        public static LoggerScopeBuilder WithChannel(this ILogger logger, ulong channelId) =>
            logger.With(LogFields.ChannelId, channelId);

        public static LoggerScopeBuilder WithChannelName(this ILogger logger, string channelName) =>
            logger.With(LogFields.ChannelName, channelName);

        public static LoggerScopeBuilder WithGuildChannel(this ILogger logger, IGuildChannel channel) =>
            logger.WithChannel(channel).WithGuild(channel.GuildId);

        public static LoggerScopeBuilder WithUser(this ILogger logger, IUser user) =>
            logger.WithUser(user.Id).WithUserName(user.Name);

        public static LoggerScopeBuilder WithUser(this ILogger logger, ulong userId) =>
            logger.With(LogFields.UserId, userId);

        public static LoggerScopeBuilder WithUserName(this ILogger logger, string userName) =>
            logger.With(LogFields.UserName, userName);

        public static LoggerScopeBuilder WithMember(this ILogger logger, IMember member) =>
            logger.WithUser(member).WithGuild(member.GuildId);

        public static LoggerScopeBuilder WithGuild(this ILogger logger, IGuild guild) =>
            logger.WithGuild(guild.Id).WithGuildName(guild.Name);

        public static LoggerScopeBuilder WithGuild(this ILogger logger, ulong guildId) =>
            logger.With(LogFields.GuildId, guildId);

        public static LoggerScopeBuilder WithGuildName(this ILogger logger, string guildName) =>
            logger.With(LogFields.GuildName, guildName);
    }
}
