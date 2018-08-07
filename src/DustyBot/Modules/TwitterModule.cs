using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System.Threading;

namespace DustyBot.Modules
{
    [Module("Twitter", "Twitter feeds and utility.", true)]
    class TwitterModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public TwitterModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
    }
}
