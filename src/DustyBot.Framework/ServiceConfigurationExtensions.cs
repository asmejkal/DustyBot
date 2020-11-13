using DustyBot.Framework.Communication;
using DustyBot.Framework.Config;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DustyBot.Framework
{
    public static class ServiceConfigurationExtensions
    {
        public static IServiceCollection AddFrameworkServices(this IServiceCollection services, Action<IServiceProvider, FrameworkConfigurationBuilder> configure)
        {
            services.AddSingleton<IFramework>(x =>
            {
                var builder = new FrameworkConfigurationBuilder(x);
                configure(x, builder);
                return new Framework(builder.Build());
            });

            services.AddSingleton<ICommunicator>(x => x.GetRequiredService<Framework>().Configuration.Communicator);
            services.AddSingleton<ILogger>(x => x.GetRequiredService<Framework>().Configuration.Logger);
            services.AddScoped<IUserFetcher, UserFetcher>();
            
            return services;
        }
    }
}
