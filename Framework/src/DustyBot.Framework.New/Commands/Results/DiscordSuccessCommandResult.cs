using System.Threading;
using System.Threading.Tasks;
using Disqord.Bot.Commands;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordSuccessCommandResult : DiscordCommandResult<IDiscordCommandContext>
    {
        public DiscordSuccessCommandResult(IDiscordCommandContext context)
            : base(context)
        {
        }

        public override Task ExecuteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
