using System;
using DustyBot.Database.Mongo;
using DustyBot.Database.Mongo.Configuration;
using DustyBot.Database.TableStorage;
using DustyBot.Database.TableStorage.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DustyBot.Database.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseServices(this IServiceCollection services,
            Action<OptionsBuilder<MongoDatabaseOptions>>? configureMongoDatabase = null,
            Action<OptionsBuilder<TableStorageOptions>>? configureTableStorage = null)
        {
            services.AddMongoDatabase(configureMongoDatabase);
            services.AddTableStorage(configureTableStorage);

            services.AddSingleton<ISettingsService, MongoSettingsService>();
            services.AddScoped<ISpotifyAccountsService, SpotifyAccountsService>();
            services.AddScoped<IDaumCafeSettingsService, DaumCafeSettingsService>();
            services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
            services.AddScoped<ILastFmSettingsService, LastFmSettingsService>();

            return services;
        }
    }
}
