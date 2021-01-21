using System;

namespace DustyBot.Framework.Exceptions
{
    public class UnclearParametersCommandException : CommandException
    {
        public UnclearParametersCommandException(string message, bool showUsage = true) : base(message) { ShowUsage = showUsage; }
        public UnclearParametersCommandException(string message, Exception inner, bool showUsage = true) : base(message, inner) { ShowUsage = showUsage; }

        public bool ShowUsage { get; }
    }
}
