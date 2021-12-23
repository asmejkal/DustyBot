using Disqord.Bot;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway.Default;
using Disqord.Hosting;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Startup;
using DustyBot.Service.Configuration;
using DustyBot.Service.Modules;
using DustyBot.Service.Services;
using DustyBot.Service.Services.Automod;
using DustyBot.Service.Services.Bot;
using DustyBot.Service.Services.GreetBye;
using DustyBot.Service.Services.Log;
using DustyBot.Service.Services.YouTube;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace DustyBot.Service
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBotServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddBotOptions(configuration);

            services.AddDatabaseServices(
                x => x.Configure<IConfiguration>((o, c) => c.GetSection(ConfigurationSections.Mongo).Bind(o)),
                x => x.Configure<IConfiguration>((o, c) => c.GetSection(ConfigurationSections.TableStorage).Bind(o)));

            // services.AddGreetByeServices();
            // services.AddAutomodServices();
            // services.AddLogServices();
            // services.AddYouTubeServices();
            services.AddBotServices();
            services.AddInfoServices();
            services.AddUtilityServices();

            return services;
        }

        public static IServiceCollection AddBotOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InteractivityExtensionConfiguration>(x => x.ConfigureInteractivity());
            services.Configure<DefaultGatewayCacheProviderConfiguration>(x => x.ConfigureCaching());
            services.Configure<CommandServiceConfiguration>(x => x.ConfigureCommands(configuration));

            services.Configure<BotOptions>(configuration.GetSection(ConfigurationSections.Bot));
            services.Configure<DiscordOptions>(configuration.GetSection(ConfigurationSections.Discord));
            services.Configure<LastFmOptions>(configuration.GetSection(ConfigurationSections.LastFm));
            services.Configure<PapagoOptions>(configuration.GetSection(ConfigurationSections.Papago));
            services.Configure<PolrOptions>(configuration.GetSection(ConfigurationSections.Polr));
            services.Configure<SpotifyOptions>(configuration.GetSection(ConfigurationSections.Spotify));
            services.Configure<YouTubeOptions>(configuration.GetSection(ConfigurationSections.YouTube));
            services.Configure<LoggingOptions>(configuration.GetSection(ConfigurationSections.Logging));
            services.Configure<WebOptions>(configuration.GetSection(ConfigurationSections.Web));

            return services;
        }

        public static IServiceCollection AddGreetByeServices(this IServiceCollection services)
        {
            services.AddDiscordModule<GreetByeModule>();
            services.AddDiscordClientService<GreetByeService>();
            services.AddScoped<IGreetByeService>(x => x.GetRequiredService<GreetByeService>());
            services.AddScoped<IGreetByeMessageBuilder, GreetByeMessageBuilder>();

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

            return services;
        }

        public static IServiceCollection AddYouTubeServices(this IServiceCollection services)
        {
            services.AddDiscordModule<YouTubeModule>();
            services.AddHttpClient<IYouTubeClient, YouTubeClient>();
            services.AddScoped<IYouTubeService, YouTubeService>();

            return services;
        }

        public static IServiceCollection AddBotServices(this IServiceCollection services)
        {
            services.AddDiscordModule<BotModule>();
            services.AddPrefixProvider<GuildPrefixProvider>();
            services.AddDiscordClientService<ThreadJoinService>();
            services.AddScoped<HelpBuilder>();
            services.AddScoped<ICommandUsageBuilder>(x => x.GetRequiredService<HelpBuilder>());
            services.AddScoped<WebLinkResolver>();

            return services;
        }

        public static IServiceCollection AddInfoServices(this IServiceCollection services)
        {
            services.AddDiscordModule<InfoModule>();

            return services;
        }

        public static IServiceCollection AddUtilityServices(this IServiceCollection services)
        {
            services.AddScoped<IUrlShortenerService, PolrUrlShortenerService>();
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();

            return services;
        }
    }
}
