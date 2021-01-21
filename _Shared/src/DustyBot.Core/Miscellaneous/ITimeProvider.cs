using System;

namespace DustyBot.Core.Miscellaneous
{
    public interface ITimeProvider
    {
        DateTimeOffset Now { get; }
        DateTimeOffset UtcNow { get; }
    }
}