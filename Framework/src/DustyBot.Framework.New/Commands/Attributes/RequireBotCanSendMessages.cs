using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    public class RequireBotCanSendMessages : DiscordGuildParameterCheckAttribute
    {
        public RequireBotCanSendMessages()
        {
        }

        public override bool CheckType(Type type)
            => typeof(IMessageGuildChannel).IsAssignableFrom(type);

        public override ValueTask<CheckResult> CheckAsync(object argument, DiscordGuildCommandContext context)
        {
            var channel = (IMessageGuildChannel)argument;
            return context.Guild.GetBotPermissions(channel).SendMessages ?
                Success() :
                Failure($"The bot doesn't have permission to send messages in {Mention.Channel(channel)}.");
        }
    }
}
