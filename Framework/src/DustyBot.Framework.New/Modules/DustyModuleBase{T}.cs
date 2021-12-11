using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Commands.Results;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyModuleBase<T> : DiscordModuleBase<T>
        where T : DiscordCommandContext
    {
        protected virtual DiscordSuccessCommandResult Success()
            => new(Context);

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

        protected virtual DiscordFailureResponseCommandResult Failure(string content)
            => Failure(new LocalMessage().WithContent(content));

        protected virtual DiscordFailureResponseCommandResult Failure(params LocalEmbed[] embeds)
            => Failure(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordFailureResponseCommandResult Failure(string content, params LocalEmbed[] embeds)
            => Failure(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordFailureResponseCommandResult Failure(LocalMessage message)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            return new(Context, message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId));
        }

        protected override DiscordResponseCommandResult Response(LocalMessage message)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            return new DiscordCacheEnabledResponseCommandResult(Context, message);
        }
    }
}
