using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            services.AddMigration(new MongoMigrationSettings()
            {
                DatabaseMigrationVersion = DatabaseConstants.RuntimeVersion
            });

            return services;
        }
    }
}
