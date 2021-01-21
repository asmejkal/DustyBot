using System;

namespace DustyBot.Service.Helpers.DaumCafe.Exceptions
{
    public class LoginFailedException : ArgumentException
    {
        public LoginFailedException() { }
        public LoginFailedException(string message) { }
        public LoginFailedException(string message, Exception innerException) { }
    }
}
