using Discord;
using Discord.WebSocket;
using DustyBot.Configuration;
using DustyBot.Framework.Utility;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

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

        public async Task LaunchAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _options.Value.BotToken);
            await _client.StartReadyAsync();
        }
    }
}
