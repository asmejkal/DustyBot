using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    interface ICommandRouter : ICommandHandlerCollection
    {
        void Register(ICommandHandler handler);
    }
}
