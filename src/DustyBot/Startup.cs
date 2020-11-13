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

            services.AddFrameworkServices((provider, builder) => 
            {
                builder.WithDiscordClient(client)
                    .WithDefaultPrefix(config.DefaultCommandPrefix)
                    .WithLogger(provider.GetRequiredService<ILogger>())
                    .WithGuildConfigProvider(provider.GetRequiredService<IFrameworkGuildConfigProvider>())
                    .AddOwners(config.OwnerIDs)
                    .AddModule<AdministrationModule>()
                    .AddModule<AutorolesModule>()
                    .AddModule<BotModule>()
                    .AddModule<CafeModule>()
                    .AddModule<EventsModule>()
                    .AddModule<InfoModule>()
                    .AddModule<InstagramModule>()
                    .AddModule<LastFmModule>()
                    .AddModule<LogModule>()
                    .AddModule<NotificationsModule>()
                    .AddModule<PollModule>()
                    .AddModule<RaidProtectionModule>()
                    .AddModule<ReactionsModule>()
                    .AddModule<RolesModule>()
                    .AddModule<ScheduleModule>()
                    .AddModule<SpotifyModule>()
                    .AddModule<StarboardModule>()
                    .AddModule<TranslatorModule>()
                    .AddModule<ViewsModule>();
            });
        }
    }
}
