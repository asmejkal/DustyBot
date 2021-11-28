using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;
using DustyBot.Database.Mongo.Collections.Templates;
using Mongo.Migration.Documents.Attributes;

namespace DustyBot.Database.Mongo.Collections.GreetBye
{
    [RuntimeVersion("2.0.0")]
    public class GreetByeSettings : BaseServerSettings
    {
        public Dictionary<GreetByeEventType, GreetByeEventSetting> Events { get; set; } = new();
    }
}
