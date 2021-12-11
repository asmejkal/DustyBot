using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Client;

namespace DustyBot.Framework.Commands.Results
{
    public class DiscordCacheEnabledResponseCommandResult : DiscordResponseCommandResult
    {
        public DiscordCacheEnabledResponseCommandResult(DiscordCommandContext context, LocalMessage message) 
            : base(context, message)
        {
        }

        public override Task<IUserMessage> ExecuteAsync() => Context.GuildId switch
        {
            null => base.ExecuteAsync(),
            _ => Context.Bot.SendMessageAsync(Context.GuildId.Value, Context.ChannelId, Message, cancellationToken: Context.Bot.StoppingToken)
        };
    }
}
