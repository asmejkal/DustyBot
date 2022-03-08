using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    public class RequireBotChannelParameterPermissionsAttribute : DiscordGuildParameterCheckAttribute
    {
        public Permission Permissions { get; }

        public RequireBotChannelParameterPermissionsAttribute(Permission permissions)
        {
            Permissions = permissions;
        }

        public override bool CheckType(Type type)
            => typeof(IGuildChannel).IsAssignableFrom(type);

        public override ValueTask<CheckResult> CheckAsync(object argument, DiscordGuildCommandContext context)
        {
            var channel = (IGuildChannel)argument;
            var permissions = context.Guild.GetBotPermissions(channel);

            return permissions.Has(Permissions) ?
                Success() :
                Failure($"The bot is missing permissions in {Mention.Channel(channel)} ({Permissions & (~permissions)}).");
        }
    }
}
