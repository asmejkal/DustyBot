using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;
using DustyBot.Core.Parsing;
using DustyBot.Database.Mongo.Collections.Reactions;
using DustyBot.Database.Mongo.Collections.Reactions.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Client;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.Reactions
{

    public class ReactionStatistics
    {
        public string Trigger { get; }
        public int TriggerCount { get; }

        public ReactionStatistics(string trigger, int triggerCount)
        {
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            TriggerCount = triggerCount;
        }
    }
}
