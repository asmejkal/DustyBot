using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;
using DustyBot.Framework.LiteDB;
using DustyBot.Settings.LiteDB;
using CommandLine;
using System.IO;

namespace DustyBot
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    class Bot : IModuleCollection, IServiceCollection
    {
        [Verb("run", HelpText = "Run the bot.")]
        public class RunOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name. Use \"instance create\" to create a new instance.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "Password", Required = true, HelpText = "Password for this instance.")]
            public string Password { get; set; }
        }

        [Verb("instance", HelpText = "Manage bot instances.")]
        public class InstanceOptions
        {
            [Value(0, MetaName = "Task", Required = true, HelpText = "The task to perform. Tasks: \"create\" - creates a new instance, \"modify\" - modifies an existing instance, \"delete\" - deletes an instance and all its settings permanently.")]
            public string Task { get; set; }

            [Value(1, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(2, MetaName = "Password", Required = true, HelpText = "Password for instance encryption. If you are creating an instance, the password you enter here will be required for every operation with the instance.")]
            public string Password { get; set; }

            [Option("token", HelpText = "Bot token.")]
            public string Token { get; set; }

            [Option("prefix", Default = Definitions.GlobalDefinitions.DefaultPrefix, HelpText = "Command prefix.")]
            public string Prefix { get; set; }

            [Option("owners", HelpText = "Owner IDs.")]
            public IEnumerable<ulong> OwnerIDs { get; set; }

            [Option("ytkey", HelpText = "Youtube API Key.")]
            public string YouTubeKey { get; set; }
        }

        [Verb("encrypt", HelpText = "Encrypt the settings database.")]
        public class EncryptOptions
        {
            [Value(0, MetaName = "Password", Required = true, HelpText = "Password for database encryption.")]
            public string Password { get; set; }

            [Value(1, MetaName = "Path", Required = true, HelpText = "Path to the database file.")]
            public string Path { get; set; }
        }

        [Verb("decrypt", HelpText = "Decrypt the settings database.")]
        public class DecryptOptions
        {
            [Value(0, MetaName = "Password", Required = true, HelpText = "Password for database decryption.")]
            public string Password { get; set; }

            [Value(1, MetaName = "Path", Required = true, HelpText = "Path to the database file.")]
            public string Path { get; set; }
        }

        private HashSet<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private List<IService> _services;
        public IEnumerable<IService> Services => _services;

        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<RunOptions, InstanceOptions, EncryptOptions, DecryptOptions>(args)
                .MapResult(
                    (RunOptions opts) => new Bot().RunAsync(opts).GetAwaiter().GetResult(),
                    (InstanceOptions opts) => new Bot().ManageInstance(opts).GetAwaiter().GetResult(),
                    (EncryptOptions opts) => new Bot().RunEncrypt(opts),
                    (DecryptOptions opts) => new Bot().RunDecrypt(opts),
                    errs => 1);

            return result;
        }

        public async Task<int> RunAsync(RunOptions opts)
        {
            try
            {
                var clientConfig = new DiscordSocketConfig
                {
                    MessageCacheSize = 200,
                    ConnectionTimeout = int.MaxValue
                };

                //Check if this instance exists
                var instancePath = Definitions.GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");
                
                using (var client = new DiscordSocketClient(clientConfig))
                using (var settings = new SettingsProvider(instancePath, new SettingsFactory(), new Migrator(Definitions.GlobalDefinitions.SettingsVersion, new Migrations()), opts.Password))
                {
                    //Get config
                    var config = await settings.ReadGlobal<Settings.BotConfig>();

                    //Choose logger
                    var logger = new Framework.Logging.ConsoleLogger(client);

                    //Choose communicator
                    var communicator = new Framework.Communication.DefaultCommunicator(config, logger);

                    //Choose modules
                    _modules = new HashSet<IModule>();
                    _modules.Add(new Modules.SelfModule(communicator, settings, this, client));
                    _modules.Add(new Modules.CafeModule(communicator, settings));
                    _modules.Add(new Modules.ViewsModule(communicator, settings));
                    _modules.Add(new Modules.ScheduleModule(communicator, settings));
                    _modules.Add(new Modules.TwitterModule(communicator, settings));
                    _modules.Add(new Modules.RolesModule(communicator, settings, logger));
                    _modules.Add(new Modules.LogModule(communicator, settings));
                    _modules.Add(new Modules.AdministrationModule(communicator, settings));

                    //Choose services
                    _services = new List<IService>();
                    _services.Add(new Services.DaumCafeService(client, settings, logger));

                    //Init framework
                    var framework = new Framework.Framework(client, _modules, _services, config, communicator, logger);

                    await framework.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Top level exception: " + ex.ToString());
            }

            return 0;
        }

        public async Task<int> ManageInstance(InstanceOptions opts)
        {
            try
            {
                var instancePath = Definitions.GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (string.Compare(opts.Task, "create", true) == 0)
                {
                    if (File.Exists(instancePath))
                        throw new InvalidOperationException("An instance with this name already exists.");
                    
                    if (opts.OwnerIDs == null || !opts.OwnerIDs.Any() || string.IsNullOrWhiteSpace(opts.Token))
                        throw new ArgumentException("Owner IDs and bot token must be specified to create an instance.");
                    
                    using (var db = DatabaseHelpers.CreateOrOpen(instancePath, opts.Password))
                    {
                        db.Engine.UserVersion = Definitions.GlobalDefinitions.SettingsVersion;
                        db.GetCollection<Settings.BotConfig>().Insert(new Settings.BotConfig
                        {
                            BotToken = opts.Token,
                            CommandPrefix = opts.Prefix,
                            OwnerIDs = new List<ulong>(opts.OwnerIDs),
                            YouTubeKey = opts.YouTubeKey
                        });
                    }
                }
                else if (string.Compare(opts.Task, "modify", true) == 0)
                {
                    if (!File.Exists(instancePath))
                        throw new InvalidOperationException($"Instance {opts.Instance} not found");

                    using (var settings = new SettingsProvider(instancePath, new SettingsFactory(), new Migrator(Definitions.GlobalDefinitions.SettingsVersion, new Migrations()), opts.Password))
                    {
                        await settings.ModifyGlobal((Settings.BotConfig s) =>
                        {
                            if (opts.Token != null)
                                s.BotToken = opts.Token;

                            if (opts.Prefix != null)
                                s.CommandPrefix = string.IsNullOrWhiteSpace(opts.Prefix) ? Definitions.GlobalDefinitions.DefaultPrefix : s.CommandPrefix = opts.Prefix;

                            if (opts.OwnerIDs != null && opts.OwnerIDs.Count() > 0)
                                s.OwnerIDs = new List<ulong>(opts.OwnerIDs);

                            if (opts.YouTubeKey != null)
                                s.YouTubeKey = opts.YouTubeKey;
                        });
                    }
                }
                else if (string.Compare(opts.Task, "delete", true) == 0)
                {
                    if (!File.Exists(instancePath))
                        throw new InvalidOperationException($"Instance {opts.Instance} not found");

                    //Check password
                    var db = DatabaseHelpers.CreateOrOpen(instancePath, opts.Password);
                    db.Dispose();

                    File.Delete(instancePath);
                }
                else
                    throw new ArgumentException("Invalid task.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }

        public int RunEncrypt(EncryptOptions opts)
        {
            try
            {
                DatabaseHelpers.Encrypt(opts.Path, opts.Password);
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
                DatabaseHelpers.Decrypt(opts.Path, opts.Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }
    }
}
