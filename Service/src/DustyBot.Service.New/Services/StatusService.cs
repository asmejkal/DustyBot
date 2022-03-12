using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Gateway;
using DustyBot.Service.Configuration;
using DustyBot.Service.Definitions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Services
{
    public class StatusService : BackgroundService
    {
        private readonly DiscordBotBase _client;
        private readonly WebOptions _webOptions;
        private readonly BotOptions _botOptions;

        public StatusService(DiscordBotBase client, IOptions<BotOptions> botOptions, IOptions<WebOptions> webOptions)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _botOptions = botOptions.Value;
            _webOptions = webOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _client.WaitUntilReadyAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await _client.SetPresenceAsync(LocalActivity.Playing($"{_botOptions.DefaultCommandPrefix}help | {_webOptions.WebsiteShorthand}"), stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);

                await _client.SetPresenceAsync(LocalActivity.Listening(string.Create(CultureDefinitions.Display, $"{_client.GetGuilds().Count:N0} servers")), stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
