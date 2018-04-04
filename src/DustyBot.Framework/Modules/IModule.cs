using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Modules
{
    public interface IModule : Events.IEventHandler, Commands.ICommandHandler
    {
        string Name { get; }
        string Description { get; }
    }
}
