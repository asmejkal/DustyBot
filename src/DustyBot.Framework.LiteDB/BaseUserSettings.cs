using System;
using System.Collections.Generic;
using System.Text;
using DustyBot.Framework.Settings;

namespace DustyBot.Framework.LiteDB
{
    public abstract class BaseUserSettings : BaseSettings, IUserSettings
    {
        public ulong UserId { get; set; }
    }
}
