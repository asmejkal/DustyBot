using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Config;
using DustyBot.Framework.Utility;

namespace DustyBot.Framework
{
    internal sealed class Framework : IFramework
    {
        internal FrameworkConfiguration Configuration { get; }

        private CommandRoutingService _commandRoutingService;

        internal Framework(FrameworkConfiguration configuration)
        {
            Configuration = configuration;
        }

        public Task StartAsync()
        {
            _commandRoutingService = new CommandRoutingService(Configuration, new UserFetcher(Configuration.DiscordClient.Rest));

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _commandRoutingService?.Dispose();
            Configuration.Dispose();
        }
    }
}
