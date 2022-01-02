using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Services;

namespace DustyBot.Service.Services
{
    internal class ThreadJoinService : DustyBotService
    {
        protected override ValueTask OnThreadCreated(ThreadCreatedEventArgs e)
        {
            if (e.Thread == null || e.Thread.Type == ChannelType.PrivateThread)
                return default;

            return new(Bot.JoinThreadAsync(e.ThreadId, cancellationToken: Bot.StoppingToken));
        }
    }
}
