using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Services;
using CommandLine;
using DustyBot.Helpers;
using DustyBot.Settings;
using DustyBot.Definitions;
using DustyBot.Database.Services;
using DustyBot.Framework.Config;
using DustyBot.Database.Sql;
using MongoDB.Driver;
using DustyBot.Framework.Utility;
using Discord;
using DustyBot.Config;
using Discord.Rest;
using DustyBot.Services;

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
            [Value(0, MetaName = "MongoConnectionString", Required = true, HelpText = "MongoDb connection string for this deployment.")]
            public string MongoConnectionString { get; set; }
        }

        [Verb("configure", HelpText = "Configure the bot deployment.")]
        public class ConfigureOptions
        {
            [Value(0, MetaName = "MongoConnectionString", Required = true, HelpText = "MongoDb connection string for this instance.")]
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

            [Option("polrkey", HelpText = "Polr URL shortener access token.")]
            public string PolrKey { get; set; }

            [Option("polrdomain", HelpText = "Polr URL shortener site (e.g. https://dusty.link).")]
            public string PolrDomain { get; set; }

            [Option("bitlykey", HelpText = "Bit.ly generic access token (Polr preferred).")]
            public string BitlyKey { get; set; }

            [Option("proxylisturl", HelpText = "URL of a proxy list.")]
            public string ProxyListUrl { get; set; }

            [Option("proxylisttoken", HelpText = "Token for the proxy list.")]
            public string ProxyListToken { get; set; }
        }

        private ICollection<IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        private ICollection<IService> _services;
        public IEnumerable<IService> Services => _services;

        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<RunOptions, ConfigureOptions>(args)
                .MapResult(
                    (RunOptions opts) => new Bot().RunAsync(opts).GetAwaiter().GetResult(),
                    (ConfigureOptions opts) => new Bot().ConfigureAsync(opts).GetAwaiter().GetResult(),
                    errs => ReturnCode.GeneralFailure);

            return (int)result;
        }

        public async Task<ReturnCode> RunAsync(RunOptions opts)
        {
            try
            {
                var intents = GatewayIntents.DirectMessageReactions |
                    GatewayIntents.DirectMessages |
                    GatewayIntents.GuildEmojis |
                    GatewayIntents.GuildMembers |
                    GatewayIntents.GuildMessageReactions |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.GuildMessageTyping |
                    GatewayIntents.Guilds;

                var clientConfig = new DiscordSocketConfig
                {
                    MessageCacheSize = 200,
                    ConnectionTimeout = int.MaxValue,
                    ExclusiveBulkDelete = true,
                    GatewayIntents = intents
                };
                
                using (var client = new DiscordShardedClient(clientConfig))
                using (var settings = await MongoSettingsService.CreateAsync(opts.MongoConnectionString))
                using (var logger = new Framework.Logging.ConsoleLogger(client, GlobalDefinitions.GetLogFile(settings.DatabaseName)))
                using (var restClient = new DiscordRestClient(new DiscordRestConfig()))
                {
                    var components = new Framework.Framework.Components() { Client = client, Logger = logger };

                    //Get config
                    var config = await settings.ReadGlobal<BotConfig>();
                    components.Config = new FrameworkConfig(config.DefaultCommandPrefix, config.BotToken, config.OwnerIDs);
                    components.GuildConfigProvider = new FrameworkGuildConfigProvider(settings);

                    //Choose communicator
                    components.Communicator = new Framework.Communication.DefaultCommunicator(components.Config, components.Logger, components.Client);

                    // URL shortener
                    IUrlShortener shortener;
                    if (config.PolrKey != null)
                        shortener = new PolrUrlShortener(config.PolrKey, config.PolrDomain);
                    else if (config.BitlyKey != null)
                        shortener = new BitlyUrlShortener(config.BitlyKey);
                    else
                        shortener = new DefaultUrlShortener();

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

                    var scheduleService = new ScheduleService(components.Client, settings, components.Logger);
                    var userFetcher = new UserFetcher(restClient);
                    components.UserFetcher = userFetcher;

                    // Proxy services
                    IProxyService proxyService = null;
                    if (!string.IsNullOrEmpty(config.ProxyListUrl))
                    {
                        proxyService = new RotatingProxyService(config.ProxyListToken, new Uri(config.ProxyListUrl), new ProxyListService(settings), logger);
                        components.Services.Add((IService)proxyService);
                    }

                    //Choose modules
                    components.Modules.Add(new Modules.BotModule(components.Communicator, settings, this, components.Client));
                    components.Modules.Add(new Modules.ScheduleModule(components.Communicator, settings, components.Logger, client, scheduleService, userFetcher));
                    components.Modules.Add(new Modules.LastFmModule(components.Communicator, settings, lastFmServiceFactory));
                    components.Modules.Add(new Modules.SpotifyModule(components.Communicator, settings, spotifyAccountsService, config));
                    components.Modules.Add(new Modules.CafeModule(components.Communicator, settings));
                    components.Modules.Add(new Modules.ViewsModule(components.Communicator, settings));
                    components.Modules.Add(new Modules.InstagramModule(components.Communicator, settings, components.Logger, config, shortener, proxyService, components.Client));
                    components.Modules.Add(new Modules.NotificationsModule(components.Communicator, settings, components.Logger, userFetcher));
                    components.Modules.Add(new Modules.TranslatorModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.StarboardModule(components.Communicator, settings, components.Logger, userFetcher, shortener, components.Client));
                    components.Modules.Add(new Modules.PollModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.ReactionsModule(components.Communicator, settings, components.Logger, config));
                    components.Modules.Add(new Modules.RaidProtectionModule(components.Communicator, settings, components.Logger, restClient));
                    components.Modules.Add(new Modules.EventsModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.AutorolesModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.RolesModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.AdministrationModule(components.Communicator, settings, components.Logger, client, userFetcher));
                    components.Modules.Add(new Modules.LogModule(components.Communicator, settings, components.Logger));
                    components.Modules.Add(new Modules.InfoModule(components.Communicator, settings, components.Logger, userFetcher));
                    _modules = components.Modules;

                    //Choose services
                    components.Services.Add(new DaumCafeService(components.Client, settings, components.Logger));
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

                    await restClient.LoginAsync(TokenType.Bot, config.BotToken);
                    await framework.Run($"{components.Config.DefaultCommandPrefix}help | {WebConstants.WebsiteShorthand}");

                    if (stopTask != null)
                        await stopTask;
                }
            }
            catch (MongoCommandException ex) when (ex.Code == 13)
            {
                Console.WriteLine("Failed to authorize access to MongoDB. Please check your connection string.\n\n" + ex.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Top level exception: " + ex.ToString());
            }

            return ReturnCode.Success;
        }

        public async Task<ReturnCode> ConfigureAsync(ConfigureOptions opts)
        {
            try
            {
                using (var settings = await MongoSettingsService.CreateAsync(opts.MongoConnectionString))
                {
                    await settings.ModifyGlobal(async (BotConfig s) =>
                    {
                        // Required
                        if (opts.Token != null)
                            s.BotToken = opts.Token;

                        if (opts.OwnerIDs != null && opts.OwnerIDs.Count() > 0)
                            s.OwnerIDs = new List<ulong>(opts.OwnerIDs);

                        if (s.OwnerIDs == null || !s.OwnerIDs.Any() || string.IsNullOrWhiteSpace(s.BotToken))
                            throw new ArgumentException("Owner IDs and bot token must be specified.");

                        if (!string.IsNullOrEmpty(opts.Prefix))
                            s.DefaultCommandPrefix = opts.Prefix;
                        else if (string.IsNullOrEmpty(s.DefaultCommandPrefix))
                            s.DefaultCommandPrefix = GlobalDefinitions.DefaultPrefix;

                        // Optional
                        if (opts.YouTubeKey != null)
                            s.YouTubeKey = opts.YouTubeKey;

                        var gac = opts.GCalendarKey != null ? await GoogleHelpers.ParseServiceAccountKeyFile(opts.GCalendarKey) : null;
                        if (gac != null)
                            s.GCalendarSAC = gac;

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

                        if (opts.PolrKey != null)
                            s.PolrKey = opts.PolrKey;

                        if (opts.PolrDomain != null)
                            s.PolrDomain = opts.PolrDomain;

                        if (opts.BitlyKey != null)
                            s.BitlyKey = opts.BitlyKey;

                        if (opts.ProxyListUrl != null)
                            s.ProxyListUrl = opts.ProxyListUrl;

                        if (opts.ProxyListToken != null)
                            s.ProxyListToken = opts.ProxyListToken;
                    });
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
