using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;
using System.Threading;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Config;

namespace DustyBot.Framework
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    public sealed class Framework : IModuleCollection, IServiceCollection, IDisposable
    {
        public class Components
        {
            public DiscordSocketClient Client { get; set; }
            public ICollection<IModule> Modules { get; } = new HashSet<IModule>();
            public ICollection<IService> Services { get; } = new HashSet<IService>();
            public Config.FrameworkConfig Config { get; set; }
            public IFrameworkGuildConfigProvider GuildConfigProvider { get; set; }

            //Optional
            public Communication.ICommunicator Communicator { get; set; }
            public Logging.ILogger Logger { get; set; }

            public bool IsComplete()
            {
                return (Modules.Count > 0 || Services.Count > 0) &&
                    Client != null &&
                    Config != null &&
                    GuildConfigProvider != null;
            }
        }

        public event Action Ready;

        private SemaphoreSlim _awaiter = new SemaphoreSlim(0);

        private HashSet<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private List<IService> _services;
        public IEnumerable<IService> Services => _services;

        public DiscordSocketClient Client { get; private set; }
        public Config.FrameworkConfig Config { get; private set; }
        public Events.IEventRouter EventRouter { get; private set; }
        public ILogger Logger { get; }

        public Framework(Components components)
        {
            if (!components.IsComplete())
                throw new ArgumentException("Incomplete components.");

            _modules = new HashSet<IModule>(components.Modules);
            _services = new List<IService>(components.Services);
            Client = components.Client;
            Config = components.Config;

            if (components.Logger == null)
                components.Logger = new Logging.ConsoleLogger(components.Client);

            Logger = components.Logger;
            EventRouter = new Events.SocketEventRouter(components.Modules, components.Client);

            if (components.Communicator == null)
                components.Communicator = new Communication.DefaultCommunicator(components.Config, components.Logger);

            if (components.Communicator is Communication.DefaultCommunicator defaultCommunicator)
                EventRouter.Register(defaultCommunicator);
            
            EventRouter.Register(new Commands.CommandRouter(components.Modules, components.Communicator, components.Logger, components.Config, components.GuildConfigProvider, new UserFetcher(Client.Rest)));
        }

        public async Task Run(string status = "")
        {
            var s = new SemaphoreSlim(0);
            Client.Ready += () => Task.FromResult(s.Release());

            //Login and start client
            await Client.LoginAsync(TokenType.Bot, Config.BotToken);
            await Client.StartAsync();

            await s.WaitAsync();
            
            EventRouter.Start();

            foreach (var service in _services)
                await service.StartAsync();

            await Client.SetGameAsync(status);

            Ready?.Invoke();
            await _awaiter.WaitAsync();
        }

        public async Task StopAsync()
        {
            var stopTasks = new List<Task>();
            foreach (var service in _services)
                stopTasks.Add(service.StopAsync());

            await Task.WhenAll(stopTasks);

            _awaiter.Release();
        }

        public void Dispose()
        {
            ((IDisposable)_awaiter).Dispose();
        }
    }
}
