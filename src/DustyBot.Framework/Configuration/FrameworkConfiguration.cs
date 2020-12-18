using Discord.WebSocket;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Modules.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DustyBot.Framework.Configuration
{
    internal sealed class FrameworkConfiguration : IDisposable
    {
        public ICollection<ModuleInfo> Modules { get; }
        public string DefaultCommandPrefix { get; }
        public ICommunicator Communicator { get; }
        public ILogger Logger { get; }
        public IFrameworkGuildConfigProvider GuildConfigProvider { get; }
        public IReadOnlyList<ulong> OwnerIDs { get; }
        public BaseSocketClient DiscordClient { get; }
        public IServiceProvider ClientServiceProvider { get; }

        public FrameworkConfiguration(
            IServiceProvider clientServiceProvider,
            string defaultCommandPrefix, 
            IEnumerable<ulong> ownerIDs, 
            IEnumerable<ModuleInfo> modules, 
            ICommunicator communicator,
            ILogger logger, 
            IFrameworkGuildConfigProvider guildConfigProvider, 
            DiscordSocketClient discordClient)
        {
            ClientServiceProvider = clientServiceProvider ?? throw new ArgumentNullException(nameof(clientServiceProvider));

            if (string.IsNullOrWhiteSpace(defaultCommandPrefix))
                throw new ArgumentException(defaultCommandPrefix);

            DefaultCommandPrefix = defaultCommandPrefix;

            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            GuildConfigProvider = guildConfigProvider ?? throw new ArgumentNullException(nameof(guildConfigProvider));

            if (!OwnerIDs.Any())
                throw new ArgumentException(nameof(ownerIDs));

            OwnerIDs = ownerIDs?.ToList() ?? throw new ArgumentNullException(nameof(ownerIDs));

            Modules = modules?.ToList() ?? throw new ArgumentNullException(nameof(modules));

            if (!Modules.Any())
                throw new ArgumentException(nameof(modules));

            DiscordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
            Communicator = communicator ?? new Communicator(discordClient, logger);
        }

        public void Dispose()
        {
            (Communicator as Communicator)?.Dispose();
        }
    }
}
