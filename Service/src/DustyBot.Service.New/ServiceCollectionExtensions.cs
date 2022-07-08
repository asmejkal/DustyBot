using Disqord.Bot;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway.Default;
using Disqord.Hosting;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Framework.Startup;
using DustyBot.Service.Configuration;
using DustyBot.Service.Modules;
using DustyBot.Service.Services;
using DustyBot.Service.Services.Automod;
using DustyBot.Service.Services.Bot;
using DustyBot.Service.Services.DaumCafe;
using DustyBot.Service.Services.GreetBye;
using DustyBot.Service.Services.Log;
using DustyBot.Service.Services.Notifications;
using DustyBot.Service.Services.Reactions;
using DustyBot.Service.Services.YouTube;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace DustyBot.Service
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBotServices(this IServiceCollection services)
        {
            services.AddBotOptions();

            services.AddDatabaseServices(
                x => x.BindConfiguration(ConfigurationSections.Mongo),
                x => x.BindConfiguration(ConfigurationSections.TableStorage));

            services.AddGreetByeServices();
            services.AddAutomodServices();
            services.AddLogServices();
            services.AddYouTubeServices();
            services.AddManagementServices();
            services.AddInfoServices();
            services.AddNotificationsServices();
            services.AddDaumCafeServices();
            services.AddReactionsServices();

            services.AddUtilityServices();

            services.AddDiscordClientService<TestSlashModule>();

            return services;
        }

        public static IServiceCollection AddBotOptions(this IServiceCollection services)
        {
            services.Configure<InteractivityExtensionConfiguration>(x => x.ConfigureInteractivity());
            services.Configure<DefaultGatewayCacheProviderConfiguration>(x => x.ConfigureCaching());
            services.Configure<CommandServiceConfiguration>(x => x.ConfigureCommands());

            services.AddOptions<BotOptions>().BindConfiguration(ConfigurationSections.Bot);
            services.AddOptions<DiscordOptions>().BindConfiguration(ConfigurationSections.Discord);
            services.AddOptions<LastFmOptions>().BindConfiguration(ConfigurationSections.LastFm);
            services.AddOptions<PapagoOptions>().BindConfiguration(ConfigurationSections.Papago);
            services.AddOptions<PolrOptions>().BindConfiguration(ConfigurationSections.Polr);
            services.AddOptions<SpotifyOptions>().BindConfiguration(ConfigurationSections.Spotify);
            services.AddOptions<YouTubeOptions>().BindConfiguration(ConfigurationSections.YouTube);
            services.AddOptions<LoggingOptions>().BindConfiguration(ConfigurationSections.Logging);
            services.AddOptions<WebOptions>().BindConfiguration(ConfigurationSections.Web);

            return services;
        }

        public static IServiceCollection AddGreetByeServices(this IServiceCollection services)
        {
            services.AddDiscordModule<GreetByeModule>();
            services.AddDiscordClientService<GreetByeService>();
            services.AddScoped<IGreetByeService>(x => x.GetRequiredService<GreetByeService>());
            services.AddScoped<IGreetByeSender, GreetByeSender>();

            return services;
        }

        public static IServiceCollection AddAutomodServices(this IServiceCollection services)
        {
            services.AddScoped<IAutomodService, AutomodService>();

            return services;
        }

        public static IServiceCollection AddLogServices(this IServiceCollection services)
        {
            services.AddDiscordModule<LogModule>();
            services.AddDiscordClientService<LogService>();
            services.AddScoped<ILogService>(x => x.GetRequiredService<LogService>());
            services.AddScoped<ILogSender, LogSender>();

            return services;
        }

        public static IServiceCollection AddYouTubeServices(this IServiceCollection services)
        {
            services.AddDiscordModule<YouTubeModule>();
            services.AddHttpClient<IYouTubeClient, YouTubeClient>();
            services.AddScoped<IYouTubeService, YouTubeService>();

            return services;
        }

        public static IServiceCollection AddManagementServices(this IServiceCollection services)
        {
            services.AddDiscordModule<BotModule>();
            services.AddPrefixProvider<GuildPrefixProvider>();
            services.AddDiscordClientService<ThreadJoinService>();
            services.AddScoped<HelpBuilder>();
            services.AddScoped<WebLinkResolver>();
            services.AddHostedService<StatusService>();

            return services;
        }

        public static IServiceCollection AddInfoServices(this IServiceCollection services)
        {
            services.AddDiscordModule<InfoModule>();

            return services;
        }

        public static IServiceCollection AddNotificationsServices(this IServiceCollection services)
        {
            services.AddDiscordModule<NotificationsModule>();
            services.AddDiscordClientService<NotificationsService>();
            services.AddScoped<INotificationsService>(x => x.GetRequiredService<NotificationsService>());
            services.AddScoped<INotificationsSender, NotificationsSender>();

            return services;
        }

        public static IServiceCollection AddDaumCafeServices(this IServiceCollection services)
        {
            services.AddDiscordModule<DaumCafeModule>();
            services.AddDiscordClientService<DaumCafeService>();
            services.AddScoped<IDaumCafeService>(x => x.GetRequiredService<DaumCafeService>());
            services.AddScoped<IDaumCafePostSender, DaumCafePostSender>();
            services.AddSingleton<IDaumCafeSessionManager, DaumCafeSessionManager>();

            return services;
        }

        public static IServiceCollection AddReactionsServices(this IServiceCollection services)
        {
            services.AddDiscordModule<ReactionsModule>();
            services.AddDiscordClientService<ReactionsService>();
            services.AddScoped<IReactionsService>(x => x.GetRequiredService<ReactionsService>());

            return services;
        }

        public static IServiceCollection AddUtilityServices(this IServiceCollection services)
        {
            services.AddScoped<IUrlShortenerService, PolrUrlShortenerService>();
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();
            
            services.AddDiscordClientService<ChannelActivityWatcher>();
            services.AddSingleton<IChannelActivityWatcher>(x => x.GetRequiredService<ChannelActivityWatcher>());

            return services;
        }
    }
}
