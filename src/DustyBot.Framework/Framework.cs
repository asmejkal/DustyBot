using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.Parsing;
using DustyBot.Framework.Configuration;
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
            var userFetcher = new UserFetcher(Configuration.DiscordClient.Rest);
            _commandRoutingService = new CommandRoutingService(Configuration, new CommandParser(userFetcher, Configuration.Communicator), userFetcher);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _commandRoutingService?.Dispose();
            Configuration.Dispose();
        }
    }
}
