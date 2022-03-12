using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;
using DustyBot.Database.Mongo.Collections.Templates;
using Mongo.Migration.Documents.Attributes;

namespace DustyBot.Database.Mongo.Collections.GreetBye
{
    [StartUpVersion("2.0.0"), CollectionLocation(nameof(GreetByeSettings))]
    public class GreetByeSettings : BaseServerSettings
    {
        public Dictionary<GreetByeEventType, GreetByeEventSetting> Events { get; set; } = new();
    }
}
