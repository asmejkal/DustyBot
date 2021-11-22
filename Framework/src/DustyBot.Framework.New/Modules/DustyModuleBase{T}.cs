using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Commands.Results;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyModuleBase<T> : DiscordModuleBase<T>
        where T : DiscordCommandContext
    {
        protected virtual DiscordSuccessResponseCommandResult Success(string content)
            => Success(new LocalMessage().WithContent(content));

        protected virtual DiscordSuccessResponseCommandResult Success(params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordSuccessResponseCommandResult Success(string content, params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordSuccessResponseCommandResult Success(LocalMessage message)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            return new(Context, message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId));
        }
    }
}
