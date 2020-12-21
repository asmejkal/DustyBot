using Discord.WebSocket;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Framework.Configuration
{
    internal sealed class FrameworkConfiguration : IDisposable
    {
        public ICollection<ModuleInfo> Modules { get; }
        public string DefaultCommandPrefix { get; }
        public ICommunicator Communicator { get; }
        public Action<ILoggingBuilder> LoggingConfiguration { get; }
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
            Action<ILoggingBuilder> loggingConfiguration, 
            IFrameworkGuildConfigProvider guildConfigProvider,
            BaseSocketClient discordClient)
        {
            ClientServiceProvider = clientServiceProvider ?? throw new ArgumentNullException(nameof(clientServiceProvider));

            if (string.IsNullOrWhiteSpace(defaultCommandPrefix))
                throw new ArgumentException(defaultCommandPrefix);

            DefaultCommandPrefix = defaultCommandPrefix;

            LoggingConfiguration = loggingConfiguration ?? throw new ArgumentNullException(nameof(loggingConfiguration));
            GuildConfigProvider = guildConfigProvider ?? throw new ArgumentNullException(nameof(guildConfigProvider));

            OwnerIDs = ownerIDs?.ToList() ?? throw new ArgumentNullException(nameof(ownerIDs));

            if (!OwnerIDs.Any())
                throw new ArgumentException(nameof(ownerIDs));

            Modules = modules?.ToList() ?? throw new ArgumentNullException(nameof(modules));

            if (!Modules.Any())
                throw new ArgumentException(nameof(modules));

            DiscordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
            Communicator = communicator;
        }

        public void Dispose()
        {
            (Communicator as Communicator)?.Dispose();
        }
    }
}
