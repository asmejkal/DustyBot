using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Exceptions
{
    public class CommandException : Exception
    {
        public CommandException() { }
        public CommandException(string message) : base(message) { }
        public CommandException(string message, Exception inner) : base(message, inner) { }
    }
    
    public class IncorrectParametersCommandException : CommandException
    {
        public IncorrectParametersCommandException() { }
        public IncorrectParametersCommandException(string message) : base(message) { }
        public IncorrectParametersCommandException(string message, Exception inner) : base(message, inner) { }
    }
}
