using System;

namespace DustyBot.Helpers.DaumCafe.Exceptions
{
    public class InaccessibleBoardException : Exception
    {
        public InaccessibleBoardException() { }
        public InaccessibleBoardException(string message) { }
        public InaccessibleBoardException(string message, Exception innerException) { }
    }
}
