using System.Threading.Tasks;
using Disqord.Bot;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    /// <summary>
    /// Specifies that the module or command can only be executed within a direct message.
    /// </summary>
    public class RequireDirectMessageAttribute : DiscordCheckAttribute
    {
        public RequireDirectMessageAttribute()
        { 
        }

        public override ValueTask<CheckResult> CheckAsync(DiscordCommandContext context)
        {
            if (context.GuildId.HasValue)
                return Failure($"This command can only be used in a direct message.");

            return Success();
        }
    }
}
