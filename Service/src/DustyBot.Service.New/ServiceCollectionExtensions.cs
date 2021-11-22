using Disqord.Bot;
using DustyBot.Core.Miscellaneous;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Database.Services.Configuration;
using DustyBot.Service.Configuration;
using DustyBot.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DustyBot.Service
{
    internal static class ServiceCollectionExtensions
    {
        public static void AddBotServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddCommands(x => x.ConfigureCommands(configuration));
            
            // Configuration
            services.Configure<BotOptions>(configuration);
            services.Configure<DatabaseOptions>(configuration);
            services.Configure<DiscordOptions>(configuration);
            services.Configure<IntegrationOptions>(configuration);
            services.Configure<LoggingOptions>(configuration);
            services.Configure<WebOptions>(configuration);

            // Database
            services.AddMongoDatabase();
            services.AddDatabaseServices();

            // Services
            services.AddScoped<IUrlShortenerService, PolrUrlShortenerService>();

            // Background services
            // services.AddHostedService<DaumCafeService>();
            // services.AddHostedApiService<IScheduleService, ScheduleService>();

            // Modules
            /* services.AddScoped<AdministrationModule>();
            services.AddSingleton<AutorolesModule>();
            services.AddSingleton<BotModule>();
            services.AddScoped<CafeModule>();
            services.AddSingleton<GreetModule>();
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

            // Miscellaneous
            services.AddScoped<HelpBuilder>();
            services.AddScoped<WebsiteWalker>(); */
            services.AddScoped<ITimerAwaiter, TimerAwaiter>();
            services.AddScoped<ITimeProvider, TimeProvider>();
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
