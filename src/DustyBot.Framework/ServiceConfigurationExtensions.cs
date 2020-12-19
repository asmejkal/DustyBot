using DustyBot.Framework.Commands.Parsing;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Configuration;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Reflection;
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

            services.AddSingleton(x => ProxyFromFrameworkServices<ICommunicator>(x));
            services.AddScoped(x => ProxyFromFrameworkServices<ICommandParser>(x));
            services.AddScoped(x => ProxyFromFrameworkServices<IUserFetcher>(x));
            services.AddScoped(x => ProxyFromFrameworkServices<IFrameworkReflector>(x));

            return services;
        }

        private static T ProxyFromFrameworkServices<T>(IServiceProvider provider) =>
                ((Framework)provider.GetRequiredService<IFramework>()).ServiceProvider.GetRequiredService<T>(); // TODO
    }
}
