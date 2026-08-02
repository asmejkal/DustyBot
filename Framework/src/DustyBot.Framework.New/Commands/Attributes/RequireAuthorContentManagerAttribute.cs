using System.Threading.Tasks;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    /// <summary>
    /// Specifies that the module or command can only be executed by guild content managers.
    /// </summary>
    public class RequireAuthorContentManagerAttribute : DiscordGuildCheckAttribute
    {
        public override ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var permissions = context.Author.CalculateGuildPermissions();
            if (permissions.HasFlag(Disqord.Permissions.ManageMessages))
                return new(Qmmands.Results.Success);

            return new(Qmmands.Results.Failure("Only members with the Manage Messages permission can use this command."));
        }
    }
}
