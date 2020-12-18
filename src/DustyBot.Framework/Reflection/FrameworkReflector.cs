using DustyBot.Framework.Configuration;
using DustyBot.Framework.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Framework.Reflection
{
    internal class FrameworkReflector : IFrameworkReflector
    {
        public IEnumerable<ModuleInfo> Modules => _configuration.Modules;

        private readonly FrameworkConfiguration _configuration;

        public FrameworkReflector(FrameworkConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public ModuleInfo GetModuleInfo<T>() => GetModuleInfo(typeof(T));

        public ModuleInfo GetModuleInfo(Type type) =>
            _configuration.Modules.FirstOrDefault(x => x.Type == type) ?? throw new ArgumentException($"Module of type {type} not found.", nameof(type));
    }
}
