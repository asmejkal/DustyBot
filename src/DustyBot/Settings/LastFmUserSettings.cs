using System;
using System.Collections.Generic;
using System.Text;
using DustyBot.Framework.Settings;
using DustyBot.Framework.LiteDB;
using LiteDB;
using System.Security;
using DustyBot.Helpers;

namespace DustyBot.Settings
{
    public class LastFmUserSettings : BaseUserSettings
    {
        public string LastFmUsername { get; set; }
        public bool Anonymous { get; set; }
    }
}
