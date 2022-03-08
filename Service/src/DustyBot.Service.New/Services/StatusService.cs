using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Gateway;
using DustyBot.Service.Definitions;
using Microsoft.Extensions.Hosting;

namespace DustyBot.Service.Services
{
    public class StatusService : BackgroundService
    {
        private readonly DiscordBotBase _client;

        public StatusService(DiscordBotBase client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _client.WaitUntilReadyAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await _client.SetPresenceAsync(LocalActivity.Playing(">help | dustybot.info"), stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);

                await _client.SetPresenceAsync(LocalActivity.Listening(string.Create(CultureDefinitions.Display, $"{_client.GetGuilds().Count:N0} servers")), stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
