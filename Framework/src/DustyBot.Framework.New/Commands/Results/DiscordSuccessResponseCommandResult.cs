using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordSuccessResponseCommandResult : DiscordResponseCommandResult
    {
        private readonly TimeSpan _deleteAfter;

        public DiscordSuccessResponseCommandResult(DiscordCommandContext context, LocalMessage message, TimeSpan deleteAfter = default)
            : base(context, message)
        {
            if (!string.IsNullOrEmpty(Message.Content))
                Message.Content = $"{CommunicationConstants.SuccessMarker} {Message.Content}";

            _deleteAfter = deleteAfter;
        }

        public override async Task<IUserMessage> ExecuteAsync()
        {
            var message = await Context.Bot.SendMessageAsync(Context.ChannelId, Message).ConfigureAwait(false);
            if (_deleteAfter != default && Context.GuildId != null)
            {
                await Task.Delay(_deleteAfter).ConfigureAwait(false);
                await message.DeleteAsync().ConfigureAwait(false);
            }

            return message;
        }
    }
}
