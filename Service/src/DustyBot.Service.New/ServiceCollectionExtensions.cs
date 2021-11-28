using Disqord.Gateway.Default;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Service.Configuration;
using DustyBot.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace DustyBot.Service
{
    internal static class ServiceCollectionExtensions
    {
        public static void AddBotServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configuration
            services.Configure<DefaultGatewayCacheProviderConfiguration>(x => x.ConfigureCaching());
            services.Configure<CommandServiceConfiguration>(x => x.ConfigureCommands(configuration));
            services.Configure<BotOptions>(configuration.GetSection(ConfigurationSections.Bot));
            services.Configure<DiscordOptions>(configuration.GetSection(ConfigurationSections.Discord));
            services.Configure<IntegrationOptions>(configuration.GetSection(ConfigurationSections.Integration));
            services.Configure<LoggingOptions>(configuration.GetSection(ConfigurationSections.Logging));
            services.Configure<WebOptions>(configuration.GetSection(ConfigurationSections.Web));

            // Database
            services.AddDatabaseServices(
                x => x.Configure<IConfiguration>((o, c) => c.GetSection(ConfigurationSections.Mongo).Bind(o)),
                x => x.Configure<IConfiguration>((o, c) => c.GetSection(ConfigurationSections.TableStorage).Bind(o)));

            // Services
            services.AddScoped<IUrlShortenerService, PolrUrlShortenerService>();

            // Miscellaneous
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();
        }
    }
}
