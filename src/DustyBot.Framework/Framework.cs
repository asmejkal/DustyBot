using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;

namespace DustyBot.Framework
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    public class Framework : IModuleCollection, IServiceCollection
    {
        private HashSet<Modules.IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;
        
        private List<IService> _services;
        public IEnumerable<IService> Services => _services;

        public DiscordSocketClient Client { get; private set; }
        public Config.IEssentialConfig Config { get; private set; }
        public Events.IEventRouter EventRouter { get; private set; }

        public Framework(DiscordSocketClient client, IEnumerable<Modules.IModule> modules, IEnumerable<Services.IService> services, Config.IEssentialConfig config, Communication.ICommunicator communicator = null, Logging.ILogger logger = null)
        {
            _modules = new HashSet<Modules.IModule>(modules);
            _services = new List<IService>(services);
            Client = client;
            Config = config;

            if (logger == null)
                logger = new Logging.ConsoleLogger(client);

            EventRouter = new Events.SocketEventRouter(modules, client);

            if (communicator == null)
                communicator = new Communication.DefaultCommunicator(config, logger);

            if (communicator is Communication.DefaultCommunicator defaultCommunicator)
                EventRouter.Register(defaultCommunicator);

            var commandRouter = new Commands.CommandRouter(modules, communicator, logger, config);
            EventRouter.Register(commandRouter);
        }

        public async Task Run()
        {
            //Login and start client
            await Client.LoginAsync(TokenType.Bot, Config.BotToken);
            await Client.StartAsync();

            foreach (var service in _services)
                service.Start();

            // Block this task until the program is closed.
            await Task.Delay(-1); //TODO: shutdown - allow for proper cleanup
        }

        public void Shutdown()
        {
            foreach (var service in _services)
                service.Stop();
        }
    }
}
