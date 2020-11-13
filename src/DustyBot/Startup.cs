using Discord.WebSocket;
using DustyBot.Config;
using DustyBot.Framework;
using DustyBot.Framework.Config;
using DustyBot.Framework.Logging;
using DustyBot.Helpers;
using DustyBot.Modules;
using DustyBot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DustyBot
{
    internal static class Startup
    {
        public static void ConfigureServices(IServiceCollection services, DiscordSocketClient client, BotConfig config)
        {
            services.AddSingleton(client);
            services.AddScoped<ILogger, SerilogLogger>();
            services.AddScoped<IFrameworkGuildConfigProvider, FrameworkGuildConfigProvider>();

            services.AddSingleton<AdministrationModule>();
            services.AddSingleton<AutorolesModule>();
            services.AddSingleton<BotModule>();
            services.AddSingleton<CafeModule>();
            services.AddSingleton<EventsModule>();
            services.AddSingleton<InfoModule>();
            services.AddSingleton<InstagramModule>();
            services.AddSingleton<LastFmModule>();
            services.AddSingleton<LogModule>();
            services.AddSingleton<NotificationsModule>();
            services.AddSingleton<PollModule>();
            services.AddSingleton<RaidProtectionModule>();
            services.AddSingleton<ReactionsModule>();
            services.AddSingleton<RolesModule>();
            services.AddSingleton<ScheduleModule>();
            services.AddSingleton<SpotifyModule>();
            services.AddSingleton<StarboardModule>();
            services.AddSingleton<TranslatorModule>();
            services.AddSingleton<ViewsModule>();

            services.AddFrameworkServices((provider, builder) => 
            {
                builder.WithDiscordClient(client)
                    .WithDefaultPrefix(config.DefaultCommandPrefix)
                    .WithLogger(provider.GetRequiredService<ILogger>())
                    .WithGuildConfigProvider(provider.GetRequiredService<IFrameworkGuildConfigProvider>())
                    .AddOwners(config.OwnerIDs)
                    .AddModulesFromServices(services);
            });
        }
    }
}
