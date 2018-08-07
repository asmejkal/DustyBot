using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.Commands
{
    [Flags]
    public enum CommandFlags
    {
        None = 0,
        RunAsync = 1 << 0,
        OwnerOnly = 1 << 1,
        Hidden = 1 << 2,
        DirectMessageAllow = 1 << 3,
        DirectMessageOnly = 1 << 4,
        TypingIndicator = 1 << 5
    }
}
