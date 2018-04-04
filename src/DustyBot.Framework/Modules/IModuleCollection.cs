using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Modules
{
    public interface IModuleCollection
    {
        IEnumerable<IModule> Modules { get; }
    }
}
