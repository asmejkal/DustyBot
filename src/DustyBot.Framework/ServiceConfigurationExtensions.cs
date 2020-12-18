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
            
            services.AddSingleton<ICommunicator>(x => x.GetRequiredService<Framework>().Configuration.Communicator);
            services.AddSingleton<ILogger>(x => x.GetRequiredService<Framework>().Configuration.Logger);
            services.AddScoped<ICommandParser>(x => new CommandParser(x.GetRequiredService<IUserFetcher>(), x.GetRequiredService<Framework>().Configuration.Communicator));
            services.AddScoped<IUserFetcher>(x => new UserFetcher(x.GetRequiredService<Framework>().Configuration.DiscordClient.Rest));
            services.AddScoped<IFrameworkReflector>(x => new FrameworkReflector(x.GetRequiredService<Framework>().Configuration));

            return services;
        }
    }
}
