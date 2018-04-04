using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public interface ICommandHandler
    {
        IEnumerable<CommandRegistration> HandledCommands { get; }
    }
}
