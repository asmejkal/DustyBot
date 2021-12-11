using System;

namespace DustyBot.Framework.Exceptions
{
    public class MissingPermissionsException : Exception
    {
        public MissingPermissionsException() 
        { 
        }

        public MissingPermissionsException(string message) : base(message) 
        {
        }

        public MissingPermissionsException(string message, Exception inner) : base(message, inner) 
        {
        }
    }
}
