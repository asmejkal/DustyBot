using System;

namespace DustyBot.Helpers.DaumCafe.Exceptions
{
    public class CountryBlockException : Exception
    {
        public CountryBlockException() { }
        public CountryBlockException(string message) { }
        public CountryBlockException(string message, Exception innerException) { }
    }
}
