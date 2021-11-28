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
    }
}
