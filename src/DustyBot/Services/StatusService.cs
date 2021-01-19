using Discord.WebSocket;
using DustyBot.Configuration;
using DustyBot.Definitions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    internal class StatusService : BackgroundService
    {
        private readonly IOptions<BotOptions> _options;
        private readonly BaseSocketClient _client;

        public StatusService(IOptions<BotOptions> options, BaseSocketClient client)
        {
            _options = options;
            _client = client;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _client.SetGameAsync($"{_options.Value.DefaultCommandPrefix}help | {WebConstants.WebsiteShorthand}");
        }
    }
}
