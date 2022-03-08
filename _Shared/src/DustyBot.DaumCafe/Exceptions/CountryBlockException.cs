using System;

namespace DustyBot.Service.Helpers.DaumCafe.Exceptions
{
    public class CountryBlockException : Exception
    {
        public CountryBlockException() { }
        public CountryBlockException(string message) { }
        public CountryBlockException(string message, Exception innerException) { }
    }
}
