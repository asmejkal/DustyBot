using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using DustyBot.Helpers;
using DustyBot.Settings;
using DustyBot.Definitions;
using DustyBot.Database.Services;
using MongoDB.Driver;
using DustyBot.Framework.Utility;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using DustyBot.Core.Async;
using DustyBot.Framework;

namespace DustyBot
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    class Bot
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

            [Option("eslognode", HelpText = "Elastic Search node uri for structured logging.")]
            public string ElasticSearchLogNodeUri { get; set; }
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
        }

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
                using var logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.Elasticsearch(opts.ElasticSearchLogNodeUri, autoRegisterTemplate: true, autoRegisterTemplateVersion: AutoRegisterTemplateVersion.ESv7)
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .CreateLogger();

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

                using var client = new DiscordSocketClient(clientConfig)
                    .UseSerilog(logger);

                using var settings = await MongoSettingsService.CreateAsync(opts.MongoConnectionString);

                var config = await settings.ReadGlobal<BotConfig>();
                var readyTask = client.WaitForReady();
                await client.LoginAsync(TokenType.Bot, config.BotToken);
                await client.StartAsync();

                await readyTask;

                TaskHelper.FireForget(() => client.SetGameAsync($"{config.DefaultCommandPrefix}help | {WebConstants.WebsiteShorthand}"));
                
                using var host = new HostBuilder()
                    .UseSerilog(logger)
                    .ConfigureServices(x => Startup.ConfigureServices(x, client, config))
                    .UseConsoleLifetime()
                    .Build();

                await host.StartAsync();

                await host.Services.GetRequiredService<IFramework>().StartAsync();

                await host.WaitForShutdownAsync();

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
