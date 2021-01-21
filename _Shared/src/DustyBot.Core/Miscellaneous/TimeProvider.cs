using System;

namespace DustyBot.Core.Miscellaneous
{
    public class TimeProvider : ITimeProvider
    {
        public DateTimeOffset Now => DateTimeOffset.Now;
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
