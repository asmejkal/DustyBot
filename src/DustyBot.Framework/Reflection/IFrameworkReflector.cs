using DustyBot.Framework.Modules;
using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Reflection
{
    public interface IFrameworkReflector
    {
        IEnumerable<ModuleInfo> Modules { get; }

        ModuleInfo GetModuleInfo<T>();
        ModuleInfo GetModuleInfo(Type type);
    }
}
