using System.Threading.Tasks;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    /// <summary>
    /// Specifies that the module or command can only be executed by guild administrators.
    /// </summary>
    public class RequireAuthorAdministratorAttribute : DiscordGuildCheckAttribute
    {
        public override ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var permissions = context.Author.CalculateGuildPermissions();
            if (permissions.HasFlag(Disqord.Permissions.Administrator))
                return new(Qmmands.Results.Success);

            return new(Qmmands.Results.Failure("Only server administrators can use this command."));
        }
    }
}
