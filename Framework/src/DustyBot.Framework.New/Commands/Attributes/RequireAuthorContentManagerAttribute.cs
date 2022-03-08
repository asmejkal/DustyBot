using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Gateway;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    /// <summary>
    /// Specifies that the module or command can only be executed by guild content managers.
    /// </summary>
    public class RequireAuthorContentManagerAttribute : DiscordGuildCheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(DiscordGuildCommandContext context)
        {
            var permissions = context.Author.GetPermissions();
            if (permissions.ManageMessages)
                return Success();

            return Failure("Only members with the Manage Messages permission can use this command.");
        }
    }
}
