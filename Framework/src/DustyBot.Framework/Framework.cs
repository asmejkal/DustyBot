using System;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DustyBot.Framework
{
    internal sealed class Framework : IFramework
    {
        internal FrameworkConfiguration Configuration { get; }
        internal IServiceProvider ServiceProvider { get; }

        internal Framework(FrameworkConfiguration configuration)
        {
            Configuration = configuration;

            var services = new ServiceCollection();
            Startup.ConfigureServices(services, configuration, this);
            ServiceProvider = services.BuildServiceProvider();
        }

        public Task StartAsync(CancellationToken ct)
        {
            var commandService = ServiceProvider.GetRequiredService<CommandRoutingService>();
            return commandService.StartAsync(ct);
        }

        public void Dispose()
        {
            ((IDisposable)ServiceProvider)?.Dispose();
            Configuration.Dispose();
        }
    }
}
