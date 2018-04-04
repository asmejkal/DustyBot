using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    interface ICommandHandlerCollection
    {
        IEnumerable<ICommandHandler> Handlers { get; }
    }
}
