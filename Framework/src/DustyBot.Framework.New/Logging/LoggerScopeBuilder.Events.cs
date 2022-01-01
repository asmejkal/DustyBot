using Disqord.Bot.Hosting;
using Disqord.Gateway;

namespace DustyBot.Framework.Logging
{
    public sealed partial class LoggerScopeBuilder
    {
        public LoggerScopeBuilder WithArgs(BanCreatedEventArgs e) =>
            WithGuild(e.GuildId).WithUser(e.User);

        public LoggerScopeBuilder WithArgs(BanDeletedEventArgs e) =>
            WithGuild(e.GuildId).WithUser(e.User);

        public LoggerScopeBuilder WithArgs(ChannelCreatedEventArgs e) =>
            WithGuild(e.GuildId).WithChannel(e.Channel);

        public LoggerScopeBuilder WithArgs(ChannelDeletedEventArgs e) =>
            WithGuild(e.GuildId).WithChannel(e.Channel);

        public LoggerScopeBuilder WithArgs(ChannelPinsUpdatedEventArgs e) => e switch
        {
            { GuildId: not null, Channel: not null } => WithGuild(e.GuildId.Value).WithChannel(e.Channel),
            _ => WithChannel(e.ChannelId)
        };

        public LoggerScopeBuilder WithArgs(MemberJoinedEventArgs e) =>
            WithGuild(e.GuildId).WithMember(e.Member);

        public LoggerScopeBuilder WithArgs(MemberLeftEventArgs e) =>
            WithGuild(e.GuildId).WithUser(e.User);

        public LoggerScopeBuilder WithArgs(MessageDeletedEventArgs e) => e switch
        {
            { GuildId: not null } => WithGuild(e.GuildId.Value).WithMessage(e.Message),
            _ => WithMessage(e.Message)
        };

        public LoggerScopeBuilder WithArgs(MessagesDeletedEventArgs e) =>
            WithGuild(e.GuildId).WithChannel(e.ChannelId);

        public LoggerScopeBuilder WithArgs(BotMessageReceivedEventArgs e) => e switch
        {
            { GuildId: not null, Channel: not null } => WithGuild(e.GuildId.Value).WithChannel(e.Channel).WithMessage(e.Message),
            _ => WithChannel(e.ChannelId).WithMessage(e.Message)
        };

        public LoggerScopeBuilder WithArgs(TypingStartedEventArgs e) => e switch
        {
            { GuildId: not null, Member: not null } => WithGuild(e.GuildId.Value).WithChannel(e.ChannelId).WithMember(e.Member),
            _ => WithChannel(e.ChannelId)
        };
    }
}
