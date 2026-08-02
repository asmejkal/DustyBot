using System.Threading.Tasks;
using Disqord.Bot.Commands;
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

        public override ValueTask<IResult> CheckAsync(IDiscordCommandContext context)
        {
            if (context.GuildId.HasValue)
                return new(Qmmands.Results.Failure("This command can only be used in a direct message."));

            return new(Qmmands.Results.Success);
        }
    }
}
