using System;

namespace DustyBot.Framework.Exceptions
{
    public class IncorrectParametersCommandException : CommandException
    {
        public IncorrectParametersCommandException(bool showUsage = true) { ShowUsage = showUsage; }
        public IncorrectParametersCommandException(string message, bool showUsage = true) : base(message) { ShowUsage = showUsage; }
        public IncorrectParametersCommandException(string message, Exception inner, bool showUsage = true) : base(message, inner) { ShowUsage = showUsage; }

        public bool ShowUsage { get; }
    }
}
