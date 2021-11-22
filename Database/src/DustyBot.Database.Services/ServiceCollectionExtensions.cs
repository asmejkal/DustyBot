using Microsoft.Extensions.DependencyInjection;

namespace DustyBot.Database.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
        {
            services.AddSingleton<ISettingsService, MongoSettingsService>();
            services.AddScoped<ISpotifyAccountsService, SpotifyAccountsService>();
            services.AddScoped<ICredentialsService, CredentialsService>();
            services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
            services.AddScoped<ILastFmSettingsService, LastFmSettingsService>();

            return services;
        }
    }
}
