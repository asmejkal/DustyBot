using System;

namespace DustyBot.Service.Helpers.DaumCafe.Exceptions
{
    public class InvalidBoardLinkException : Exception
    {
        public InvalidBoardLinkException() { }
        public InvalidBoardLinkException(string message) { }
        public InvalidBoardLinkException(string message, Exception innerException) { }
    }
}
