using Discord.WebSocket;
using DustyBot.Config;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Database.Sql;
using DustyBot.Framework;
using DustyBot.Framework.Configuration;
using DustyBot.Framework.Logging;
using DustyBot.Helpers;
using DustyBot.Modules;
using DustyBot.Services;
using DustyBot.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;

namespace DustyBot
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, BaseSocketClient client, BotConfig config, string mongoConnectionString)
        {
            services.AddSingleton(client);
            services.AddSingleton(config);
            services.AddScoped<IFrameworkGuildConfigProvider, FrameworkGuildConfigProvider>();
            services.AddTransient<ITimerAwaiter, TimerAwaiter>();

            // Database
            services.AddSingleton<ISettingsService>(x => new MongoSettingsService(mongoConnectionString));
            services.AddScoped<IProxyListService, ProxyListService>();
            services.AddScoped<ISpotifyAccountsService>(x => new SpotifyAccountsService(config.TableStorageConnectionString));
            services.AddTransient(x => DustyBotDbContext.Create(config.SqlDbConnectionString));
            services.AddScoped<Func<ILastFmStatsService>>(x => () => ActivatorUtilities.CreateInstance<LastFmStatsService>(x));

            // Services
            services.AddHostedService<DaumCafeService>();
            services.AddHostedApiService<IScheduleService, ScheduleService>();
            services.AddHostedApiService<IProxyService, RotatingProxyService>(x =>
                ActivatorUtilities.CreateInstance<RotatingProxyService>(x, config.ProxyListToken, new Uri(config.ProxyListUrl)));

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

            services.AddFrameworkServices((provider, builder) => 
            {
                builder.WithDiscordClient(client)
                    .WithDefaultPrefix(config.DefaultCommandPrefix)
                    .ConfigureLogging(x => x.AddSerilog(provider.GetRequiredService<ILogger>()))
                    .WithGuildConfigProvider(provider.GetRequiredService<IFrameworkGuildConfigProvider>())
                    .AddOwners(config.OwnerIDs)
                    .AddModulesFromServices(services);
            });
        }
    }
}
