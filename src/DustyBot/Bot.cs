using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;

namespace DustyBot
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    class Bot : IModuleCollection, IServiceCollection
    {
        private HashSet<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private List<IService> _services;
        public IEnumerable<IService> Services => _services;

        public static void Main(string[] args)
            => new Bot().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 200,
                ConnectionTimeout = int.MaxValue
            });

            //Choose settings provider
            var settings = new Framework.LiteDB.SettingsProvider(Definitions.GlobalDefinitions.SettingsPath, new Settings.LiteDB.SettingsFactory());

            //Choose config parser
            var config = await Settings.JSON.JsonConfig.Create();

            //Choose logger
            var logger = new Framework.Logging.ConsoleLogger(client);

            //Choose communicator
            var communicator = new Framework.Communication.DefaultCommunicator(config);

            //Choose modules
            _modules = new HashSet<IModule>();
            _modules.Add(new Modules.SelfModule(communicator, config, this));
            _modules.Add(new Modules.AdministrationModule(communicator, settings));
            _modules.Add(new Modules.MediaModule(communicator, settings, config));
            _modules.Add(new Modules.RolesModule(communicator, settings, logger));
            _modules.Add(new Modules.LogModule(communicator, settings));

            //Choose services
            _services = new List<IService>();
            _services.Add(new Services.DaumCafeService(client, settings, config, logger));

            //Init framework
            var framework = new Framework.Framework(client, _modules, _services, config, communicator, logger);

            await framework.Run();
        }
    }
}
