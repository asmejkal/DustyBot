using System;
using System.Collections.Generic;
using System.Linq;
using Discord.WebSocket;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Configuration
{
    internal sealed class FrameworkConfiguration : IDisposable
    {
        public ICollection<ModuleInfo> Modules { get; }
        public string DefaultCommandPrefix { get; }
        public ICommunicator Communicator { get; }
        public Action<ILoggingBuilder> LoggingConfiguration { get; }
        public Func<IFrameworkGuildConfigProvider> GuildConfigProviderFactory { get; }
        public IReadOnlyList<ulong> OwnerIDs { get; }
        public IGatewayClient GatewayClient { get; }
        public IRestClient RestClient { get; }
        public IServiceProvider ClientServiceProvider { get; }

        public FrameworkConfiguration(
            IServiceProvider clientServiceProvider,
            string defaultCommandPrefix, 
            IEnumerable<ulong> ownerIDs, 
            IEnumerable<ModuleInfo> modules, 
            ICommunicator communicator,
            Action<ILoggingBuilder> loggingConfiguration, 
            Func<IFrameworkGuildConfigProvider> guildConfigProviderFactory,
            BaseSocketClient discordClient)
        {
            ClientServiceProvider = clientServiceProvider ?? throw new ArgumentNullException(nameof(clientServiceProvider));

            if (string.IsNullOrWhiteSpace(defaultCommandPrefix))
                throw new ArgumentException(defaultCommandPrefix);

            DefaultCommandPrefix = defaultCommandPrefix;

            LoggingConfiguration = loggingConfiguration ?? throw new ArgumentNullException(nameof(loggingConfiguration));
            GuildConfigProviderFactory = guildConfigProviderFactory ?? throw new ArgumentNullException(nameof(guildConfigProviderFactory));

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
