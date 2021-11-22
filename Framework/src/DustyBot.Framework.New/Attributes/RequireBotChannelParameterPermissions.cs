using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Qmmands;

namespace DustyBot.Framework.Attributes
{
    public class RequireBotChannelParameterPermissions : DiscordGuildParameterCheckAttribute
    {
        public Permission Permissions { get; }

        public RequireBotChannelParameterPermissions(Permission permissions)
        {
            Permissions = permissions;
        }

        public override bool CheckType(Type type)
            => typeof(IGuildChannel).IsAssignableFrom(type);

        public override ValueTask<CheckResult> CheckAsync(object argument, DiscordGuildCommandContext context)
        {
            var channel = (IGuildChannel)argument;
            var botMember = context.Guild.GetMember(context.Bot.CurrentUser.Id);
            var roles = botMember.GetRoles();
            var permissions = Discord.Permissions.CalculatePermissions(context.Guild, channel, botMember, roles.Values);

            return permissions.Has(Permissions) ?
                Success() :
                Failure($"The bot is missing permissions in {Mention.Channel(channel)} ({Permissions - permissions}).");
        }
    }
}
