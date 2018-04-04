using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace DustyBot.Definitions
{
    public static class GlobalDefinitions
    {
        public const string DataFolder = "Data";
        public const string SettingsFile = "Settings.db";
        public static string SettingsPath => Path.Combine(DataFolder, SettingsFile);
    }
}
