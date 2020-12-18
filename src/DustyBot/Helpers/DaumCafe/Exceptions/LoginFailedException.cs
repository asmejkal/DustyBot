using System;

namespace DustyBot.Helpers.DaumCafe.Exceptions
{
    public class LoginFailedException : ArgumentException
    {
        public LoginFailedException() { }
        public LoginFailedException(string message) { }
        public LoginFailedException(string message, Exception innerException) { }
    }
}
