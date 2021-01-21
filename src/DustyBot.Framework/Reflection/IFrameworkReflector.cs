using System;
using System.Collections.Generic;
using DustyBot.Framework.Modules;

namespace DustyBot.Framework.Reflection
{
    public interface IFrameworkReflector
    {
        IEnumerable<ModuleInfo> Modules { get; }

        ModuleInfo GetModuleInfo<T>();
        ModuleInfo GetModuleInfo(Type type);
    }
}
