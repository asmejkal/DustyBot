using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;
using CommandLine;
using System.IO;
using DustyBot.Helpers;
using DustyBot.Settings;
using DustyBot.Definitions;
using DustyBot.Database.Services;
using DustyBot.Database.Services.Exceptions;
using DustyBot.Framework.Config;
using DustyBot.Database.Sql;
using DustyBot.Database.Mongo.Management;

namespace DustyBot
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    class Bot : IModuleCollection, IServiceCollection
    {
        public enum ReturnCode
        {
            Success = 0,
            GeneralFailure = 1,
            InstanceNotFound = 2
        }

        [Verb("run", HelpText = "Run the bot.")]
        public class RunOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name. Use \"instance create\" to create a new instance.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "MongoConnectionString", Required = true, HelpText = "MongoDb connection string for this instance.")]
            public string MongoConnectionString { get; set; }
        }

        [Verb("instance", HelpText = "Manage bot instances.")]
        public class InstanceOptions
        {
            [Value(0, MetaName = "Task", Required = true, HelpText = "The task to perform. Tasks: \"create\" - creates a new instance, \"modify\" - modifies an existing instance, \"delete\" - deletes an instance and all its settings permanently.")]
            public string Task { get; set; }

            [Value(1, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(2, MetaName = "MongoConnectionString", Required = true, HelpText = "MongoDb connection string for this instance.")]
            public string MongoConnectionString { get; set; }

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

            [Option("sqldbcs", HelpText = "SQL DB connection string.")]
            public string SqlDbConnectionString { get; set; }
        
            [Option("papagoclientid", HelpText = "Papago client id string.")]
            public string PapagoClientId { get; set; }

            [Option("papagoclientsecret", HelpText = "Papago client secret string.")]
            public string PapagoClientSecret { get; set; }
        }

        private ICollection<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private ICollection<IService> _services;
        public IEnumerable<IService> Services => _services;

        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<RunOptions, InstanceOptions>(args)
                .MapResult(
                    (RunOptions opts) => new Bot().RunAsync(opts).GetAwaiter().GetResult(),
                    (InstanceOptions opts) => new Bot().ManageInstance(opts).GetAwaiter().GetResult(),
                    errs => ReturnCode.GeneralFailure);

            return (int)result;
        }

        public async Task<ReturnCode> RunAsync(RunOptions opts)
        {
            try
            {
                var clientConfig = new DiscordSocketConfig
                {
                    MessageCacheSize = 200,
                    ConnectionTimeout = int.MaxValue,
                    ExclusiveBulkDelete = true,
                    AlwaysDownloadUsers = true
                };
                
                using (var client = new DiscordSocketClient(clientConfig))
                using (var settings = await MongoSettingsService.CreateAsync(opts.MongoConnectionString, opts.Instance))
                using (var logger = new Framework.Logging.ConsoleLogger(client, GlobalDefinitions.GetLogFile(opts.Instance)))
                {
                    var components = new Framework.Framework.Components() { Client = client, Logger = logger };

                    //Get config
                    var config = await settings.ReadGlobal<BotConfig>();
                    components.Config = new FrameworkConfig(config.CommandPrefix, config.BotToken, config.OwnerIDs);

                    //Choose communicator
                    components.Communicator = new Framework.Communication.DefaultCommunicator(components.Config, components.Logger);

                    // Sql services
                    var sqlConnectionString = config.SqlDbConnectionString;
                    Func<Task<ILastFmStatsService>> lastFmServiceFactory = null;
                    if (!string.IsNullOrEmpty(sqlConnectionString))
                    {
                        lastFmServiceFactory = new Func<Task<ILastFmStatsService>>(() => 
                            Task.FromResult<ILastFmStatsService>(new LastFmStatsService(DustyBotDbContext.Create(sqlConnectionString))));
                    }

                    // Table storage services
                    SpotifyAccountsService spotifyAccountsService = null;
                    if (!string.IsNullOrEmpty(config.TableStorageConnectionString))
                        spotifyAccountsService = new SpotifyAccountsService(config.TableStorageConnectionString);

                    //Choose modules
                    var scheduleService = new Services.ScheduleService(components.Client, settings, components.Logger);
                    components.Modules.Add(new Modules.BotModule(components.Communicator, settings, this, components.Client));
                    components.Modules.Add(new Modules.ScheduleModule(components.Communicator, settings, components.Logger, client, scheduleService));
                    components.Modules.Add(new Modules.LastFmModule(components.Communicator, settings, lastFmServiceFactory));
                    components.Modules.Add(new Modules.SpotifyModule(components.Communicator, settings, spotifyAccountsService, config));
                    components.Modules.Add(new Modules.CafeModule(components.Communicator, settings));
                    components.Modules.Add(new Modules.ViewsModule(components.Communicator, settings));
                    components.Modules.Add(new Modules.InstagramModule(components.Communicator, settings, components.Logger, config));
                    components.Modules.Add(new Modules.NotificationsModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.TranslatorModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.StarboardModule(components.Communicator, settings, components.Logger, config));
                    components.Modules.Add(new Modules.PollModule(components.Communicator, settings, components.Logger, config));
                    components.Modules.Add(new Modules.ReactionsModule(components.Communicator, settings, components.Logger, config));
                    components.Modules.Add(new Modules.RaidProtectionModule(components.Communicator, settings, components.Logger, client.Rest));
                    components.Modules.Add(new Modules.EventsModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.AutorolesModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.RolesModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.AdministrationModule(components.Communicator, settings, components.Logger, client));
                    components.Modules.Add(new Modules.LogModule(components.Communicator, settings, components.Logger, client));
                    components.Modules.Add(new Modules.InfoModule(components.Communicator, settings, components.Logger));
                    _modules = components.Modules;

                    //Choose services
                    components.Services.Add(new Services.DaumCafeService(components.Client, settings, components.Logger));
                    components.Services.Add(scheduleService);
                    _services = components.Services;

                    //Init framework
                    var framework = new Framework.Framework(components);

                    Task stopTask = null;
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        stopTask = framework.StopAsync();
                    };

                    await framework.Run($"{components.Config.CommandPrefix}help | {WebConstants.WebsiteShorthand}");

                    if (stopTask != null)
                        await stopTask;
                }
            }
            catch (DatabaseNotFoundException)
            {
                Console.WriteLine($"Instance {opts.Instance} not found. Use \"instance create\" to create an instance.");
                return ReturnCode.InstanceNotFound;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Top level exception: " + ex.ToString());
            }

            return ReturnCode.Success;
        }

        public async Task<ReturnCode> ManageInstance(InstanceOptions opts)
        {
            try
            {
                if (string.Compare(opts.Task, "create", true) == 0)
                {
                    if (opts.OwnerIDs == null || !opts.OwnerIDs.Any() || string.IsNullOrWhiteSpace(opts.Token))
                        throw new ArgumentException("Owner IDs and bot token must be specified to create an instance.");

                    if (!Directory.Exists(GlobalDefinitions.DataFolder))
                        Directory.CreateDirectory(GlobalDefinitions.DataFolder);

                    using (var settings = await MongoSettingsService.CreateAsync(opts.MongoConnectionString, opts.Instance, createIfNotExists: true))
                    {
                        await settings.ModifyGlobal(async (BotConfig s) =>
                        {
                            s.BotToken = opts.Token;
                            s.CommandPrefix = string.IsNullOrWhiteSpace(opts.Prefix) ? GlobalDefinitions.DefaultPrefix : opts.Prefix;
                            s.OwnerIDs = new List<ulong>(opts.OwnerIDs);
                            s.YouTubeKey = opts.YouTubeKey;
                            s.GCalendarSAC = opts.GCalendarKey != null ? await GoogleHelpers.ParseServiceAccountKeyFile(opts.GCalendarKey) : null;
                            s.ShortenerKey = opts.ShortenerKey;
                            s.LastFmKey = opts.LastFmKey;
                            s.SpotifyId = opts.SpotifyId;
                            s.SpotifyKey = opts.SpotifyKey;
                            s.TableStorageConnectionString = opts.TableStorageConnectionString;
                            s.SqlDbConnectionString = opts.SqlDbConnectionString;
                            s.PapagoClientId = opts.PapagoClientId;
                            s.PapagoClientSecret = opts.PapagoClientSecret;
                        });
                    }
                }
                else if (string.Compare(opts.Task, "modify", true) == 0)
                {
                    var googleAccountCredentials = opts.GCalendarKey != null ? await GoogleHelpers.ParseServiceAccountKeyFile(opts.GCalendarKey) : null;

                    using (var settings = await MongoSettingsService.CreateAsync(opts.MongoConnectionString, opts.Instance))
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

                            if (googleAccountCredentials != null)
                                s.GCalendarSAC = googleAccountCredentials;

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

                            if (opts.SqlDbConnectionString != null)
                                s.SqlDbConnectionString = opts.SqlDbConnectionString;

                            if (opts.PapagoClientId != null)
                                s.PapagoClientId = opts.PapagoClientId;

                            if (opts.PapagoClientSecret != null)
                                s.PapagoClientSecret = opts.PapagoClientSecret;
                        });
                    }
                }
                else if (string.Compare(opts.Task, "delete", true) == 0)
                {
                    await MongoDatabaseManager.DropDatabaseAsync(opts.MongoConnectionString, opts.Instance);
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
    }
}
