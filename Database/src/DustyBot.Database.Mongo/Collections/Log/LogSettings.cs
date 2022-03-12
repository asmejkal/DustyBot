using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using Mongo.Migration.Documents.Attributes;

namespace DustyBot.Database.Mongo.Collections.Log
{
    [StartUpVersion("2.0.0"), CollectionLocation(nameof(LogSettings))]
    public class LogSettings : BaseServerSettings
    {
        public ulong MessageDeletedChannel { get; set; }
        public HashSet<ulong> MessageDeletedChannelFilters { get; set; } = new();
        public HashSet<string> MessageDeletedPrefixFilters { get; set; } = new();
    }
}
