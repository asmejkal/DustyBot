using System;

namespace DustyBot.Helpers.DaumCafe.Exceptions
{
    public class InvalidBoardLinkException : Exception
    {
        public InvalidBoardLinkException() { }
        public InvalidBoardLinkException(string message) { }
        public InvalidBoardLinkException(string message, Exception innerException) { }
    }
}
