using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Services
{
    internal class StatusService : BackgroundService
    {
        private readonly BaseSocketClient _client;
        private readonly IOptions<BotOptions> _botOptions;
        private readonly IOptions<WebOptions> _webOptions;

        public StatusService(BaseSocketClient client, IOptions<BotOptions> botOptions, IOptions<WebOptions> webOptions)
        {
            _client = client;
            _botOptions = botOptions;
            _webOptions = webOptions;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _client.SetGameAsync($"{_botOptions.Value.DefaultCommandPrefix}help | {_webOptions.Value.WebsiteShorthand}");
        }
    }
}
