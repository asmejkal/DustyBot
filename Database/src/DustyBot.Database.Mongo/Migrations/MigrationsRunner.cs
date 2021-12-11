using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Mongo.Migration;

namespace DustyBot.Database.Mongo.Migrations
{
    public class MigrationsRunner : IHostedService
    {
        private readonly IMongoMigration _migration;

        public MigrationsRunner(IMongoMigration migration)
        {
            _migration = migration ?? throw new ArgumentNullException(nameof(migration));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _migration.Run();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
