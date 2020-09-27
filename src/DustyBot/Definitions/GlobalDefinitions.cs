﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

namespace DustyBot.Definitions
{
    public static class GlobalDefinitions
    {
        public const string DefaultPrefix = ">";
        public const string DataFolder = "Data";
        public static string GetLogFile(string instance) => Path.Combine(DataFolder, instance + ".log");
        
        public static readonly CultureInfo Culture = new CultureInfo("en-US");
    }
}
