using System;

namespace DustyBot.Framework.Interactivity
{
    [Flags]
    public enum TableColumnFlags
    {
        None = 0,
        Unquoted = 1 << 0,
    }
}
