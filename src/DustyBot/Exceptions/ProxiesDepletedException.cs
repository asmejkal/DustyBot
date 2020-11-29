using System;

namespace DustyBot.Exceptions
{
    public class ProxiesDepletedException : Exception
    {
        public ProxiesDepletedException() { }
        public ProxiesDepletedException(string message) : base(message) { }
        public ProxiesDepletedException(string message, Exception inner) : base(message, inner) { }
    }
}
