using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;
using DustyBot.Settings.LiteDB;
using CommandLine;

namespace DustyBot
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    class Bot : IModuleCollection, IServiceCollection
    {
        [Verb("run", HelpText = "Runs the bot.")]
        public class RunOptions
        {
            [Value(0, MetaName = "Password", Required = true, HelpText = "Password for database encryption. If this is your first time running the bot, the password you enter now will be required on every startup.")]
            public string Password { get; set; }
        }

        [Verb("encrypt", HelpText = "Encrypts the settings database.")]
        public class EncryptOptions
        {
            [Value(0, MetaName = "Password", Required = true, HelpText = "Password for database encryption.")]
            public string Password { get; set; }

            [Value(1, MetaName = "Path", Default = null, Required = false, HelpText = "Path to the database file.")]
            public string Path { get; set; }
        }

        [Verb("decrypt", HelpText = "Decrypts the settings database.")]
        public class DecryptOptions
        {
            [Value(0, MetaName = "Password", Required = true, HelpText = "Password for database decryption.")]
            public string Password { get; set; }

            [Value(1, MetaName = "Path", Default = null, Required = false, HelpText = "Path to the database file.")]
            public string Path { get; set; }
        }

        private HashSet<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private List<IService> _services;
        public IEnumerable<IService> Services => _services;

        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<RunOptions, EncryptOptions, DecryptOptions>(args)
                .MapResult(
                    (RunOptions opts) => new Bot().RunAsync(opts).GetAwaiter().GetResult(),
                    (EncryptOptions opts) => new Bot().RunEncrypt(opts),
                    (DecryptOptions opts) => new Bot().RunDecrypt(opts),
                    errs => 1);

            return result;
        }

        public async Task<int> RunAsync(RunOptions opts)
        {
            try
            {
                var client = new DiscordSocketClient(new DiscordSocketConfig
                {
                    MessageCacheSize = 200,
                    ConnectionTimeout = int.MaxValue
                });

                //Choose settings provider
                var migrator = new Framework.LiteDB.Migrator(Definitions.GlobalDefinitions.SettingsVersion, new Migrations());
                var settings = new Framework.LiteDB.SettingsProvider(Definitions.GlobalDefinitions.SettingsPath, new SettingsFactory(), migrator, opts.Password);

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
            catch (Exception ex)
            {
                Console.WriteLine("Top level exception: " + ex.ToString());
            }

            return 0;
        }

        public int RunEncrypt(EncryptOptions opts)
        {
            try
            {
                Framework.LiteDB.DatabaseHelpers.Encrypt(opts.Path ?? Definitions.GlobalDefinitions.SettingsPath, opts.Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }
            
            return 0;
        }

        public int RunDecrypt(DecryptOptions opts)
        {
            try
            {
                Framework.LiteDB.DatabaseHelpers.Decrypt(opts.Path ?? Definitions.GlobalDefinitions.SettingsPath, opts.Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }
    }
}
