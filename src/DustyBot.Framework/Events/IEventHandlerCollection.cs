using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Events
{
    interface IEventHandlerCollection
    {
        IEnumerable<IEventHandler> Handlers { get; }
    }
}
