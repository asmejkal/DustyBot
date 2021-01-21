using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Discord.WebSocket;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Modules.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Configuration
{
    public sealed class FrameworkConfigurationBuilder
    {
        private readonly IServiceProvider _clientServiceProvider;

        private HashSet<Type> _modules = new HashSet<Type>();
        private string _defaultCommandPrefix;
        private HashSet<ulong> _ownerIDs = new HashSet<ulong>();
        private ICommunicator _communicator;
        private Action<ILoggingBuilder> _loggingConfiguration;
        private IFrameworkGuildConfigProvider _guildConfigProvider;
        private BaseSocketClient _discordClient;

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

        public FrameworkConfigurationBuilder AddModulesFromServices(IServiceCollection services)
        {
            _modules.UnionWith(services.Where(x => x.ServiceType.GetCustomAttribute<ModuleAttribute>() != null).Select(x => x.ServiceType));
            return this;
        }

        public FrameworkConfigurationBuilder AddModulesFromAssembly(Assembly assembly)
        {
            _modules.UnionWith(assembly.GetTypes().Where(x => x.GetCustomAttribute<ModuleAttribute>() != null));
            return this;
        }

        public FrameworkConfigurationBuilder AddModulesFromExecutingAssembly() => 
            AddModulesFromAssembly(Assembly.GetCallingAssembly());

        public FrameworkConfigurationBuilder AddOwner(ulong id)
        {
            _ownerIDs.Add(id);
            return this;
        }

        public FrameworkConfigurationBuilder AddOwners(IEnumerable<ulong> ids)
        {
            _ownerIDs.UnionWith(ids);
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

        public FrameworkConfigurationBuilder ConfigureLogging(Action<ILoggingBuilder> loggingConfiguration)
        {
            _loggingConfiguration = loggingConfiguration;
            return this;
        }

        public FrameworkConfigurationBuilder WithGuildConfigProvider(IFrameworkGuildConfigProvider guildConfigProvider)
        {
            _guildConfigProvider = guildConfigProvider;
            return this;
        }

        public FrameworkConfigurationBuilder WithDiscordClient(BaseSocketClient discordClient)
        {
            _discordClient = discordClient;
            return this;
        }

        internal FrameworkConfiguration Build()
        {
            var modules = _modules.Select(x => ModuleInfo.Create(x));

            return new FrameworkConfiguration(
                _clientServiceProvider, 
                _defaultCommandPrefix, 
                _ownerIDs,
                modules,
                _communicator, 
                _loggingConfiguration, 
                _guildConfigProvider, 
                _discordClient);
        }
    }
}
