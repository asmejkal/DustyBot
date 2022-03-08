using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    public class RequireBotCanSendEmbedsAttribute : DiscordGuildParameterCheckAttribute
    {
        public RequireBotCanSendEmbedsAttribute()
        {
        }

        public override bool CheckType(Type type)
            => typeof(IMessageGuildChannel).IsAssignableFrom(type);

        public override ValueTask<CheckResult> CheckAsync(object argument, DiscordGuildCommandContext context)
        {
            var channel = (IMessageGuildChannel)argument;
            return context.Guild.GetBotPermissions(channel).SendEmbeds ?
                Success() :
                Failure($"The bot doesn't have permission to send embeds (embed links) in {Mention.Channel(channel)}.");
        }
    }
}
