using Discord.Rest;
using Discord.WebSocket;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.Parsing;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Configuration;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using Microsoft.Extensions.DependencyInjection;

namespace DustyBot.Framework
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, FrameworkConfiguration configuration, Framework framework)
        {
            services.AddSingleton(configuration);
            services.AddSingleton(framework);
            services.AddSingleton(configuration.Logger);
            services.AddSingleton(configuration.GuildConfigProvider);
            services.AddSingleton(configuration.DiscordClient);
            services.AddTransient<DiscordRestClient>(x => x.GetRequiredService<BaseSocketClient>().Rest);

            if (configuration.Communicator != null)
                services.AddSingleton(configuration.Communicator);
            else
                services.AddSingleton<ICommunicator, Communicator>();

            services.AddSingleton<CommandRoutingService>();
            services.AddSingleton<ICommandParser, CommandParser>();
            services.AddSingleton<IUserFetcher, UserFetcher>();
            services.AddScoped<IFrameworkReflector, FrameworkReflector>();
        }
    }
}
