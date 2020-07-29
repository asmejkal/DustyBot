using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.Commands
{
    [Flags]
    public enum ParameterFlags
    {
        None = 0,
        Optional = 1 << 0,
        Remainder = 1 << 1,
        Repeatable = 1 << 2,
        Hidden = 1 << 3,
    }
}
