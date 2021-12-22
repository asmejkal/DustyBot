using Disqord.Bot;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway.Default;
using Disqord.Hosting;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Framework.Communication;
using DustyBot.Service.Configuration;
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
            // Configuration
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

            // Database
            services.AddDatabaseServices(
                x => x.Configure<IConfiguration>((o, c) => c.GetSection(ConfigurationSections.Mongo).Bind(o)),
                x => x.Configure<IConfiguration>((o, c) => c.GetSection(ConfigurationSections.TableStorage).Bind(o)));

            // Greet & Bye
            services.AddDiscordClientService<GreetByeService>();
            services.AddScoped<IGreetByeService>(x => x.GetRequiredService<GreetByeService>());
            services.AddScoped<IGreetByeMessageBuilder, GreetByeMessageBuilder>();

            // Automod
            services.AddScoped<IAutomodService, AutomodService>();

            // Log
            services.AddDiscordClientService<LogService>();
            services.AddScoped<ILogService>(x => x.GetRequiredService<LogService>());

            // YouTube
            services.AddHttpClient<IYouTubeClient, YouTubeClient>();
            services.AddScoped<IYouTubeService, YouTubeService>();

            // Bot
            services.AddPrefixProvider<GuildPrefixProvider>();
            services.AddDiscordClientService<ThreadJoinService>();
            services.AddScoped<HelpBuilder>();
            services.AddScoped<ICommandUsageBuilder>(x => x.GetRequiredService<HelpBuilder>());
            services.AddScoped<WebLinkResolver>();

            // Miscellaneous
            services.AddScoped<IUrlShortenerService, PolrUrlShortenerService>();
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();

            return services;
        }
    }
}
