using Disqord.Gateway.Default;
using Disqord.Hosting;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Service.Configuration;
using DustyBot.Service.Services;
using DustyBot.Service.Services.Automod;
using DustyBot.Service.Services.GreetBye;
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

            // Discord services
            services.AddDiscordClientService<GreetByeService>();

            // Greet & Bye
            services.AddScoped<IGreetByeService>(x => x.GetRequiredService<GreetByeService>());
            services.AddScoped<IGreetByeMessageBuilder, GreetByeMessageBuilder>();

            // Automod
            services.AddScoped<IAutomodService, AutomodService>();

            // Miscellaneous
            services.AddScoped<IUrlShortenerService, PolrUrlShortenerService>();
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();

            return services;
        }
    }
}
