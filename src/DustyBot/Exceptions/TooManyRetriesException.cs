using System;

namespace DustyBot.Exceptions
{
    public class TooManyRetriesException : Exception
    {
        public TooManyRetriesException() { }
        public TooManyRetriesException(string message) : base(message) { }
        public TooManyRetriesException(string message, Exception inner) : base(message, inner) { }
    }
}
