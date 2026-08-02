using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    public class RequireBotCanSendMessagesAttribute : DiscordGuildParameterCheckAttribute
    {
        public RequireBotCanSendMessagesAttribute()
        {
        }

        public override bool CanCheck(IParameter parameter, object? value)
            => value is IMessageGuildChannel;

        public override ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context, IParameter parameter, object? argument)
        {
            var channel = (IMessageGuildChannel)argument!;
            var guild = context.Bot.GetGuild(context.GuildId);
            return new(guild is not null && guild.GetBotPermissions(channel).HasFlag(Permissions.SendMessages)
                ? Qmmands.Results.Success
                : Qmmands.Results.Failure($"The bot doesn't have permission to send messages in {Mention.Channel(channel)}."));
        }
    }
}
