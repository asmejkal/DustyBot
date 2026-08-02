using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Text;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordFailureResponseCommandResult : DiscordTextResponseCommandResult
    {
        public DiscordFailureResponseCommandResult(IDiscordCommandContext context, LocalMessage message)
            : base(context, message)
        {
            if (Message.Content.HasValue)
                Message.Content = $"{CommunicationConstants.FailureMarker} {Message.Content}";
        }
    }
}
