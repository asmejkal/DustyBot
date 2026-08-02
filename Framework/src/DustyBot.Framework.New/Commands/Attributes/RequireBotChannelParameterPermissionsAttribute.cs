using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    public class RequireBotChannelParameterPermissionsAttribute : DiscordGuildParameterCheckAttribute
    {
        public Permissions Permissions { get; }

        public RequireBotChannelParameterPermissionsAttribute(Permissions permissions)
        {
            Permissions = permissions;
        }

        public override bool CanCheck(IParameter parameter, object? value)
            => value is IGuildChannel;

        public override ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context, IParameter parameter, object? argument)
        {
            var channel = (IGuildChannel)argument!;
            var guild = context.Bot.GetGuild(context.GuildId);
            if (guild is null)
                return new(Qmmands.Results.Failure("Guild is not cached."));

            var permissions = guild.GetBotPermissions(channel);
            return new(permissions.HasFlag(Permissions)
                ? Qmmands.Results.Success
                : Qmmands.Results.Failure($"The bot is missing permissions in {Mention.Channel(channel)} ({Permissions & ~permissions})."));
        }
    }
}
