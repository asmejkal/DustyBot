using Disqord;

namespace DustyBot.Framework.Logging
{
    public sealed partial class LoggerScopeBuilder
    {
        public LoggerScopeBuilder WithMessage(IMessage message) =>
            WithMessage(message.Id).WithUser(message.Author).WithChannel(message.ChannelId);

        public LoggerScopeBuilder WithMessage(ulong messageId) =>
            With(LogFields.MessageId, messageId);

        public LoggerScopeBuilder WithChannel(IChannel channel) =>
            WithChannel(channel.Id).WithChannelName(channel.Name);

        public LoggerScopeBuilder WithChannel(ulong channelId) =>
            With(LogFields.ChannelId, channelId);

        public LoggerScopeBuilder WithChannelName(string channelName) =>
            With(LogFields.ChannelName, channelName);

        public LoggerScopeBuilder WithGuildChannel(IGuildChannel channel) =>
            WithChannel(channel).WithGuild(channel.GuildId);

        public LoggerScopeBuilder WithUser(IUser user) =>
            WithUser(user.Id).WithUserName(user.Name);

        public LoggerScopeBuilder WithUser(ulong userId) =>
            With(LogFields.UserId, userId);

        public LoggerScopeBuilder WithUserName(string userName) =>
            With(LogFields.UserName, userName);

        public LoggerScopeBuilder WithMember(IMember member) =>
            WithUser(member).WithGuild(member.GuildId);

        public LoggerScopeBuilder WithGuild(IGuild guild) =>
            WithGuild(guild.Id).WithGuildName(guild.Name);

        public LoggerScopeBuilder WithGuild(ulong guildId) =>
            With(LogFields.GuildId, guildId);

        public LoggerScopeBuilder WithGuildName(string guildName) =>
            With(LogFields.GuildName, guildName);
    }
}
