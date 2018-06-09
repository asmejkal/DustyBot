using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;
using System.IO;

namespace DustyBot
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    class Bot : IModuleCollection
    {
        private HashSet<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        public static void Main(string[] args)
            => new Bot().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = 200,
                ConnectionTimeout = int.MaxValue
            });

            //Choose communicator
            var communicator = new Framework.Communication.DefaultCommunicator();

            //Choose settings provider
            var settings = new Settings.LiteDB.SettingsProviderProxy(Definitions.GlobalDefinitions.SettingsPath, new Settings.LiteDB.SettingsFactory());

            //Choose config parser
            var config = await Settings.JSON.JsonConfig.Create();

            //Choose logger
            var logger = new Framework.Logging.ConsoleLogger(client);

            //Choose modules
            _modules = new HashSet<IModule>();
            _modules.Add(new Modules.SelfModule(communicator, config, this));
            _modules.Add(new Modules.AdministrationModule(communicator, settings));
            _modules.Add(new Modules.MediaModule(communicator, settings, config));
            _modules.Add(new Modules.RolesModule(communicator, settings, logger));
            _modules.Add(new Modules.LogModule(communicator, settings));

            //Init framework
            var framework = new Framework.Framework(client, _modules, config, communicator, logger);
            
            //Login and start client
            await client.LoginAsync(TokenType.Bot, config.BotToken);
            await client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}
