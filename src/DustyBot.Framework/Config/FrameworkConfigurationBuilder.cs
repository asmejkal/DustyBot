using Discord.WebSocket;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Config
{
    public sealed class FrameworkConfigurationBuilder
    {
        private readonly IServiceProvider _clientServiceProvider;

        private HashSet<Type> _modules = new HashSet<Type>();
        private string _defaultCommandPrefix;
        private HashSet<ulong> _ownerIDs = new HashSet<ulong>();
        private ICommunicator _communicator;
        private ILogger _logger;
        private IFrameworkGuildConfigProvider _guildConfigProvider;
        private DiscordSocketClient _discordClient;

        public FrameworkConfigurationBuilder(IServiceProvider clientServiceProvider)
        {
            _clientServiceProvider = clientServiceProvider ?? throw new ArgumentNullException(nameof(clientServiceProvider));
        }

        public FrameworkConfigurationBuilder AddModule<T>()
            where T : class
        {
            _modules.Add(typeof(T));
            return this;
        }

        public FrameworkConfigurationBuilder AddOwner(ulong id)
        {
            _ownerIDs.Add(id);
            return this;
        }

        public FrameworkConfigurationBuilder AddOwners(IEnumerable<ulong> ids)
        {
            _ownerIDs.IntersectWith(ids);
            return this;
        }

        public FrameworkConfigurationBuilder WithDefaultPrefix(string prefix)
        {
            _defaultCommandPrefix = prefix;
            return this;
        }

        public FrameworkConfigurationBuilder WithCommunicator(ICommunicator communicator)
        {
            _communicator = communicator;
            return this;
        }

        public FrameworkConfigurationBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        public FrameworkConfigurationBuilder WithGuildConfigProvider(IFrameworkGuildConfigProvider guildConfigProvider)
        {
            _guildConfigProvider = guildConfigProvider;
            return this;
        }

        public FrameworkConfigurationBuilder WithDiscordClient(DiscordSocketClient discordClient)
        {
            _discordClient = discordClient;
            return this;
        }

        internal FrameworkConfiguration Build()
        {
            return new FrameworkConfiguration(_clientServiceProvider, _defaultCommandPrefix, _ownerIDs, _modules, _communicator, _logger, _guildConfigProvider, _discordClient);
        }
    }
}
