using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Events
{
    public interface IEventHandlerCollection
    {
        IEnumerable<IEventHandler> Handlers { get; }
    }
}
