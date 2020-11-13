using DustyBot.Framework.Communication;
using System;

namespace DustyBot.Framework.Exceptions
{
    public class AbortException : Exception
    {
        public AbortException() : base("") { }
        public AbortException(string message) : base(message) { }
        public AbortException(string message, Exception inner) : base(message, inner) { }

        public AbortException(PageCollection pages) : base()
        {
            Pages = pages;
        }

        public PageCollection Pages { get; }
    }
}
