using System.Threading.Tasks;
using Disqord.Bot;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordSuccessCommandResult : DiscordCommandResult
    {
        public DiscordSuccessCommandResult(DiscordCommandContext context)
            : base(context)
        {
        }

        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}
