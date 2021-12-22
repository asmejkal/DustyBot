using System.Threading.Tasks;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Services;

namespace DustyBot.Service.Services
{
    internal class ThreadJoinService : DustyBotService
    {
        protected override ValueTask OnThreadCreated(ThreadCreatedEventArgs e) => 
            new(Bot.JoinThreadAsync(e.ThreadId, cancellationToken: Bot.StoppingToken));
    }
}
