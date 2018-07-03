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
        public class Components
        {
            public DiscordSocketClient Client { get; set; }
            public ICollection<IModule> Modules { get; } = new HashSet<IModule>();
            public ICollection<IService> Services { get; } = new HashSet<IService>();
            public Settings.ISettingsProvider Settings { get; set; }
            public Config.IEssentialConfig Config { get; set; }

            //Optional
            public Communication.ICommunicator Communicator { get; set; }
            public Logging.ILogger Logger { get; set; }

            public bool IsComplete()
            {
                return (Modules.Count > 0 || Services.Count > 0) &&
                    Client != null &&
                    Settings != null &&
                    Config != null;
            }
        }

        private HashSet<Modules.IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private List<IService> _services;
        public IEnumerable<IService> Services => _services;

        public DiscordSocketClient Client { get; private set; }
        public Config.IEssentialConfig Config { get; private set; }
        public Events.IEventRouter EventRouter { get; private set; }

        public Framework(Components components)
        {
            if (!components.IsComplete())
                throw new ArgumentException("Incomplete components.");

            _modules = new HashSet<Modules.IModule>(components.Modules);
            _services = new List<IService>(components.Services);
            Client = components.Client;
            Config = components.Config;

            if (components.Logger == null)
                components.Logger = new Logging.ConsoleLogger(components.Client);

            EventRouter = new Events.SocketEventRouter(components.Modules, components.Client);

            if (components.Communicator == null)
                components.Communicator = new Communication.DefaultCommunicator(components.Config, components.Logger);

            if (components.Communicator is Communication.DefaultCommunicator defaultCommunicator)
                EventRouter.Register(defaultCommunicator);
            
            EventRouter.Register(new Commands.CommandRouter(components.Modules, components.Communicator, components.Logger, components.Config));
            EventRouter.Register(new Settings.SettingsCleaner(components.Settings, components.Logger));
        }

        public async Task Run(string status = "")
        {
            //Login and start client
            await Client.LoginAsync(TokenType.Bot, Config.BotToken);
            await Client.StartAsync();

            foreach (var service in _services)
                service.Start();

            await Client.SetGameAsync(status);

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
