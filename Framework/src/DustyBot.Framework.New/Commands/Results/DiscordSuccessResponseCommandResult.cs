using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Text;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordSuccessResponseCommandResult : DiscordTextResponseCommandResult
    {
        public DiscordSuccessResponseCommandResult(IDiscordCommandContext context, LocalMessage message)
            : base(context, message)
        {
            if (Message.Content.HasValue)
                Message.Content = $"{CommunicationConstants.SuccessMarker} {Message.Content.Value}";
        }
    }
}
