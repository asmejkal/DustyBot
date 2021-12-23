using System;
using DustyBot.Database.Mongo.Configuration;
using DustyBot.Database.Mongo.Conventions;
using DustyBot.Database.Mongo.Migrations;
using DustyBot.Database.Mongo.Serializers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mongo.Migration.Startup;
using Mongo.Migration.Startup.DotNetCore;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMongoDatabase(this IServiceCollection services,
            Action<OptionsBuilder<MongoDatabaseOptions>>? configure = null)
        {
            configure?.Invoke(services.AddOptions<MongoDatabaseOptions>());

            services.AddSingleton<IMongoClient>(x =>
            {
                ConventionsRegistrar.RegisterDefaults();
                SerializersRegistrar.RegisterDefaults();

                var options = x.GetRequiredService<IOptions<MongoDatabaseOptions>>().Value;
                var url = MongoUrl.Create(options.ConnectionString);
                return new MongoClient(url);
            });

            services.AddSingleton<IMongoDatabase>(x =>
            {
                var options = x.GetRequiredService<IOptions<MongoDatabaseOptions>>().Value;
                var url = MongoUrl.Create(options.ConnectionString);
                return x.GetRequiredService<IMongoClient>().GetDatabase(url.DatabaseName);
            });

            /* TODO
            services.AddMigration();
            services.Replace(ServiceDescriptor.Singleton<IMongoMigrationSettings>(x =>
            {
                var options = x.GetRequiredService<IOptions<MongoDatabaseOptions>>().Value;
                return new MongoMigrationSettings()
                {
                    DatabaseMigrationVersion = DatabaseConstants.RuntimeVersion,
                    ConnectionString = options.ConnectionString,
                    Database = MongoUrl.Create(options.ConnectionString).DatabaseName
                };
            }));

            services.AddHostedService<MigrationsRunner>();*/

            return services;
        }
    }
}
