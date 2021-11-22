﻿using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordSuccessResponseCommandResult : DiscordResponseCommandResult
    {
        public DiscordSuccessResponseCommandResult(DiscordCommandContext context, LocalMessage message)
            : base(context, message)
        {
            if (!string.IsNullOrEmpty(Message.Content))
                Message.Content = $"{CommunicationConstants.SuccessMarker} {Message.Content}";
        }
    }
}
