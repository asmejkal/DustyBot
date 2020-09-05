using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;
using DustyBot.Framework.LiteDB;
using DustyBot.Settings.LiteDB;
using CommandLine;
using System.IO;
using DustyBot.Helpers;
using DustyBot.Settings;
using DustyBot.Framework.Settings;
using DustyBot.Definitions;

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

            [Option("prefix", HelpText = "Command prefix.")]
            public string Prefix { get; set; }

            [Option("owners", HelpText = "Owner IDs.")]
            public IEnumerable<ulong> OwnerIDs { get; set; }

            [Option("ytkey", HelpText = "Youtube API key.")]
            public string YouTubeKey { get; set; }

            [Option("gcalendarkey", HelpText = "Google Calendar service account key file.")]
            public string GCalendarKey { get; set; }

            [Option("shortenerkey", HelpText = "Bit.ly generic access token.")]
            public string ShortenerKey { get; set; }

            [Option("lastfmkey", HelpText = "Last.fm API key.")]
            public string LastFmKey { get; set; }

            [Option("spotifyid", HelpText = "Spotify App client id.")]
            public string SpotifyId { get; set; }

            [Option("spotifykey", HelpText = "Spotify App client secret.")]
            public string SpotifyKey { get; set; }

            [Option("tablestoragecs", HelpText = "Azure Table Storage connection string.")]
            public string TableStorageConnectionString { get; set; }

            [Option("papagoclientid", HelpText = "Papago client id string.")]
            public string PapagoClientId { get; set; }

            [Option("papagoclientsecret", HelpText = "Papago client secret string.")]
            public string PapagoClientSecret { get; set; }
        }

        [Verb("encrypt", HelpText = "Encrypt an instance.")]
        public class EncryptOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "Password", Required = true, HelpText = "Password for database encryption.")]
            public string Password { get; set; }
        }

        [Verb("decrypt", HelpText = "Decrypt an instance.")]
        public class DecryptOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "Password", Required = true, HelpText = "Password for database decryption.")]
            public string Password { get; set; }
        }

        [Verb("upgrade", HelpText = "Upgrades to new database format.")]
        public class UpgradeOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "Password", Required = true, HelpText = "Password for database decryption.")]
            public string Password { get; set; }
        }

        [Verb("check-integrity", HelpText = "Checks integrity of the settings database.")]
        public class CheckIngegrityOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "Password", Required = true, HelpText = "Password for database decryption.")]
            public string Password { get; set; }
        }

        private ICollection<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private ICollection<IService> _services;
        public IEnumerable<IService> Services => _services;

        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<RunOptions, InstanceOptions, EncryptOptions, DecryptOptions, UpgradeOptions, CheckIngegrityOptions>(args)
                .MapResult(
                    (RunOptions opts) => new Bot().RunAsync(opts).GetAwaiter().GetResult(),
                    (InstanceOptions opts) => new Bot().ManageInstance(opts).GetAwaiter().GetResult(),
                    (EncryptOptions opts) => new Bot().RunEncrypt(opts),
                    (DecryptOptions opts) => new Bot().RunDecrypt(opts),
                    (UpgradeOptions opts) => new Bot().Upgrade(opts),
                    (CheckIngegrityOptions opts) => new Bot().CheckIntegrityAsync(opts).GetAwaiter().GetResult(),
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
                    ConnectionTimeout = int.MaxValue,
                    ExclusiveBulkDelete = true
                };

                //Check if this instance exists
                var instancePath = GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");
                
                using (var client = new DiscordSocketClient(clientConfig))
                using (var settings = new SettingsProvider(instancePath, new Migrator(GlobalDefinitions.SettingsVersion, new Migrations()), opts.Password))
                using (var logger = new Framework.Logging.ConsoleLogger(client, GlobalDefinitions.GetLogFile(opts.Instance)))
                {
                    var components = new Framework.Framework.Components() { Client = client, Settings = settings, Logger = logger };
                    
                    //Get config
                    components.Config = await components.Settings.ReadGlobal<BotConfig>();

                    //Choose communicator
                    components.Communicator = new Framework.Communication.DefaultCommunicator(components.Config, components.Logger);

                    //Choose modules
                    var scheduleService = new Services.ScheduleService(components.Client, components.Settings, components.Logger);
                    components.Modules.Add(new Modules.BotModule(components.Communicator, components.Settings, this, components.Client));
                    components.Modules.Add(new Modules.ScheduleModule(components.Communicator, components.Settings, components.Logger, client, scheduleService));
                    components.Modules.Add(new Modules.LastFmModule(components.Communicator, components.Settings));
                    components.Modules.Add(new Modules.SpotifyModule(components.Communicator, components.Settings, (BotConfig)components.Config));
                    components.Modules.Add(new Modules.CafeModule(components.Communicator, components.Settings));
                    components.Modules.Add(new Modules.ViewsModule(components.Communicator, components.Settings));
                    components.Modules.Add(new Modules.InstagramModule(components.Communicator, components.Settings, components.Logger, (BotConfig)components.Config));
                    components.Modules.Add(new Modules.NotificationsModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.StarboardModule(components.Communicator, components.Settings, components.Logger, (BotConfig)components.Config));
                    components.Modules.Add(new Modules.PollModule(components.Communicator, components.Settings, components.Logger, components.Config));
                    components.Modules.Add(new Modules.ReactionsModule(components.Communicator, components.Settings, components.Logger, components.Config));
                    components.Modules.Add(new Modules.RaidProtectionModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.EventsModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.AutorolesModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.RolesModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.AdministrationModule(components.Communicator, components.Settings, components.Logger, client));
                    components.Modules.Add(new Modules.LogModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.InfoModule(components.Communicator, components.Settings, components.Logger));
                    components.Modules.Add(new Modules.TranslatorModule(components.Communicator, components.Settings, components.Logger));
                    _modules = components.Modules;

                    //Choose services
                    components.Services.Add(new Services.DaumCafeService(components.Client, components.Settings, components.Logger));
                    components.Services.Add(scheduleService);
                    _services = components.Services;

                    //Init framework
                    var framework = new Framework.Framework(components);

                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        framework.Stop();
                    };

                    await framework.Run($"{components.Config.CommandPrefix}help | {WebConstants.WebsiteShorthand}");
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
                var instancePath = GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (string.Compare(opts.Task, "create", true) == 0)
                {
                    if (File.Exists(instancePath))
                        throw new InvalidOperationException("An instance with this name already exists.");
                    
                    if (opts.OwnerIDs == null || !opts.OwnerIDs.Any() || string.IsNullOrWhiteSpace(opts.Token))
                        throw new ArgumentException("Owner IDs and bot token must be specified to create an instance.");

                    if (!Directory.Exists(GlobalDefinitions.DataFolder))
                        Directory.CreateDirectory(GlobalDefinitions.DataFolder);

                    using (var db = DatabaseHelpers.CreateOrOpen(instancePath, opts.Password))
                    {
                        db.UserVersion = GlobalDefinitions.SettingsVersion;
                        db.GetCollection<BotConfig>().Insert(new BotConfig
                        {
                            BotToken = opts.Token,
                            CommandPrefix = string.IsNullOrWhiteSpace(opts.Prefix) ? GlobalDefinitions.DefaultPrefix : opts.Prefix,
                            OwnerIDs = new List<ulong>(opts.OwnerIDs),
                            YouTubeKey = opts.YouTubeKey,
                            GCalendarSAC = opts.GCalendarKey != null ? await GoogleHelpers.ParseServiceAccountKeyFile(opts.GCalendarKey) : null,
                            ShortenerKey = opts.ShortenerKey,
                            LastFmKey = opts.LastFmKey,
                            SpotifyId = opts.SpotifyId,
                            SpotifyKey = opts.SpotifyKey,
                            TableStorageConnectionString = opts.TableStorageConnectionString,
                            PapagoClientId = opts.PapagoClientId,
                            PapagoClientSecret = opts.PapagoClientSecret
                        });
                    }
                }
                else if (string.Compare(opts.Task, "modify", true) == 0)
                {
                    if (!File.Exists(instancePath))
                        throw new InvalidOperationException($"Instance {opts.Instance} not found");

                    var GCalendarSAC = opts.GCalendarKey != null ? await GoogleHelpers.ParseServiceAccountKeyFile(opts.GCalendarKey) : null;

                    using (var settings = new SettingsProvider(instancePath, new Migrator(GlobalDefinitions.SettingsVersion, new Migrations()), opts.Password))
                    {
                        await settings.ModifyGlobal((BotConfig s) =>
                        {
                            if (opts.Token != null)
                                s.BotToken = opts.Token;

                            if (!string.IsNullOrWhiteSpace(opts.Prefix))
                                s.CommandPrefix = opts.Prefix;

                            if (opts.OwnerIDs != null && opts.OwnerIDs.Count() > 0)
                                s.OwnerIDs = new List<ulong>(opts.OwnerIDs);

                            if (opts.YouTubeKey != null)
                                s.YouTubeKey = opts.YouTubeKey;

                            if (GCalendarSAC != null)
                                s.GCalendarSAC = GCalendarSAC;

                            if (opts.ShortenerKey != null)
                                s.ShortenerKey = opts.ShortenerKey;

                            if (opts.LastFmKey != null)
                                s.LastFmKey = opts.LastFmKey;

                            if (opts.SpotifyId != null)
                                s.SpotifyId = opts.SpotifyId;

                            if (opts.SpotifyKey != null)
                                s.SpotifyKey = opts.SpotifyKey;

                            if (opts.TableStorageConnectionString != null)
                                s.TableStorageConnectionString = opts.TableStorageConnectionString;
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
                var instancePath = GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");

                DatabaseHelpers.Encrypt(instancePath, opts.Password);
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
                var instancePath = GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");

                DatabaseHelpers.Decrypt(instancePath, opts.Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }

        public int Upgrade(UpgradeOptions opts)
        {
            try
            {
                var instancePath = GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");

                DatabaseHelpers.Upgrade(instancePath, opts.Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }

        public async Task<int> CheckIntegrityAsync(CheckIngegrityOptions opts)
        {
            try
            {
                var instancePath = GlobalDefinitions.GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");

                using (var settings = new SettingsProvider(instancePath, new Migrator(GlobalDefinitions.SettingsVersion, new Migrations()), opts.Password))
                {
                    async Task TestSettings(Func<Task> checker, Type type)
                    {
                        try
                        {
                            await checker();
                            Console.WriteLine($"OK: {type.Name}");
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($">> NOT OK: {type.Name}");
                        }
                    }

                    Task TestServerSettings<T>() where T : IServerSettings => TestSettings(() => settings.Read<T>(), typeof(T));

                    Task TestUserSettings<T>() where T : IUserSettings => TestSettings(() => settings.ReadUser<T>(), typeof(T));

                    await TestServerSettings<EventsSettings>();
                    await TestUserSettings<LastFmUserSettings>();
                    await TestServerSettings<LogSettings>();
                    await TestServerSettings<MediaSettings>();
                    await TestServerSettings<NotificationSettings>();
                    await TestServerSettings<PollSettings>();
                    await TestServerSettings<RaidProtectionSettings>();
                    await TestServerSettings<ReactionsSettings>();
                    await TestServerSettings<RolesSettings>();
                    await TestServerSettings<ScheduleSettings>();
                    await TestServerSettings<StarboardSettings>();
                    await TestUserSettings<UserCredentials>();
                    await TestUserSettings<UserNotificationSettings>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }
    }
}
