using System;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Database.Services.Configuration;
using DustyBot.Framework;
using DustyBot.Framework.Configuration;
using DustyBot.Service.Configuration;
using DustyBot.Service.Definitions;
using DustyBot.Service.Helpers;
using DustyBot.Service.Modules;
using DustyBot.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace DustyBot
{
    internal static class ServicesConfigurationExtensions
    {
        public static void AddBotServices(this IServiceCollection services, IConfiguration config)
        {
            // Configuration
            services.Configure<BotOptions>(config);
            services.Configure<DatabaseOptions>(config);
            services.Configure<DiscordOptions>(config);
            services.Configure<IntegrationOptions>(config);
            services.Configure<LoggingOptions>(config);
            services.Configure<WebOptions>(config);
            services.AddScoped<IFrameworkGuildConfigProvider, FrameworkGuildConfigProvider>();

            // Discord
            services.AddDiscordClient();
            services.AddTransient<DiscordClientLauncher>();

            // Database
            services.AddMongoDatabase();
            services.AddSingleton<ISettingsService, MongoSettingsService>();
            services.AddScoped<ISpotifyAccountsService, SpotifyAccountsService>();
            services.AddScoped<ICredentialsService, CredentialsService>();
            services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
            services.AddScoped<ILastFmSettingsService, LastFmSettingsService>();

            // Services
            services.AddHostedService<StatusService>();
            services.AddHostedService<DaumCafeService>();
            services.AddHostedApiService<IScheduleService, ScheduleService>();

            // Modules
            services.AddScoped<AdministrationModule>();
            services.AddSingleton<AutorolesModule>();
            services.AddSingleton<BotModule>();
            services.AddScoped<CafeModule>();
            services.AddSingleton<EventsModule>();
            services.AddScoped<InfoModule>();
            services.AddSingleton<InstagramModule>();
            services.AddScoped<LastFmModule>();
            services.AddSingleton<LogModule>();
            services.AddSingleton<NotificationsModule>();
            services.AddScoped<PollModule>();
            services.AddSingleton<RaidProtectionModule>();
            services.AddSingleton<ReactionsModule>();
            services.AddSingleton<RolesModule>();
            services.AddScoped<ScheduleModule>();
            services.AddScoped<SpotifyModule>();
            services.AddSingleton<StarboardModule>();
            services.AddScoped<TranslatorModule>();
            services.AddScoped<ViewsModule>();

            // Framework
            services.AddFrameworkServices((provider, builder) => 
            {
                var options = provider.GetRequiredService<IOptions<BotOptions>>();
                builder.WithDiscordClient(provider.GetRequiredService<BaseSocketClient>())
                    .WithDefaultPrefix(options.Value.DefaultCommandPrefix)
                    .ConfigureLogging(x => x.AddSerilog(provider.GetRequiredService<ILogger>()))
                    .WithGuildConfigProvider(provider.GetRequiredService<IFrameworkGuildConfigProvider>())
                    .AddOwner(options.Value.OwnerID)
                    .AddModulesFromServices(services);
            });

            // Miscellaneous
            services.AddScoped<IUrlShortener, PolrUrlShortener>();
            services.AddScoped<HelpBuilder>();
            services.AddScoped<WebsiteWalker>();
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();
        }

        public static void ConfigureBotLogging(this LoggerConfiguration configuration, IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<LoggingOptions>>();
            var discordOptions = provider.GetRequiredService<IOptions<DiscordOptions>>();

            configuration.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Component", "dustybot-service")
                .Enrich.WithProperty("ComponentInstance", $"shard-{string.Join("+", discordOptions.Value.Shards ?? new[] { 0 })}")
                .MinimumLevel.Information();

            if (!string.IsNullOrEmpty(options.Value.ElasticsearchNodeUri))
            {
                configuration.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(options.Value.ElasticsearchNodeUri))
                {
                    IndexFormat = "dustybot-{0:yyyy-MM-dd}",
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    DetectElasticsearchVersion = true,
                    RegisterTemplateFailure = RegisterTemplateRecovery.FailSink,
                    EmitEventFailure = EmitEventFailureHandling.ThrowException
                });
            }
        }

        private static IServiceCollection AddDiscordClient(this IServiceCollection services)
        {
            services.AddSingleton(x =>
            {
                var options = x.GetRequiredService<IOptions<DiscordOptions>>();
                var intents = GatewayIntents.DirectMessageReactions |
                    GatewayIntents.DirectMessages |
                    GatewayIntents.GuildEmojis |
                    GatewayIntents.GuildMembers |
                    GatewayIntents.GuildMessageReactions |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.GuildMessageTyping |
                    GatewayIntents.Guilds;

                var config = new DiscordSocketConfig
                {
                    MessageCacheSize = 200,
                    ExclusiveBulkDelete = true,
                    GatewayIntents = intents,
                    TotalShards = options.Value.TotalShards
                };

                return new DiscordShardedClient(options.Value.Shards, config)
                    .UseSerilog(x.GetRequiredService<ILogger>());
            });

            services.AddSingleton<BaseSocketClient>(x => x.GetRequiredService<DiscordShardedClient>());
            return services;
        }

        private static IServiceCollection AddMongoDatabase(this IServiceCollection services)
        {
            services.AddSingleton<IMongoClient>(x =>
            {
                var options = x.GetRequiredService<IOptions<DatabaseOptions>>();
                var url = MongoUrl.Create(options.Value.MongoDbConnectionString);
                return new MongoClient(url);
            });

            services.AddSingleton<IMongoDatabase>(x =>
            {
                var options = x.GetRequiredService<IOptions<DatabaseOptions>>();
                var url = MongoUrl.Create(options.Value.MongoDbConnectionString);
                return x.GetRequiredService<IMongoClient>().GetDatabase(url.DatabaseName);
            });

            return services;
        }
    }
}
