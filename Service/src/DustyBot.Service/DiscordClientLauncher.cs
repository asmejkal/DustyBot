using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Framework.Utility;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Options;

namespace DustyBot
{
    internal class DiscordClientLauncher
    {
        private readonly IOptions<DiscordOptions> _options;
        private readonly DiscordShardedClient _client;

        public DiscordClientLauncher(IOptions<DiscordOptions> options, DiscordShardedClient client)
        {
            _options = options;
            _client = client;
        }

        public async Task LaunchAsync(CancellationToken ct)
        {
            await _client.LoginAsync(TokenType.Bot, _options.Value.BotToken);
            await _client.StartReadyAsync(ct);
        }
    }
}
