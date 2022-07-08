using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Reactions.Models;
using DustyBot.Database.Mongo.Collections.Templates;
using Mongo.Migration.Documents.Attributes;

namespace DustyBot.Database.Mongo.Collections.Reactions
{
    [StartUpVersion("2.0.0"), CollectionLocation(nameof(ReactionsSettings))]
    public class ReactionsSettings : BaseServerSettings
    {
        public List<Reaction> Reactions { get; set; } = new();
        public int NextReactionId { get; set; } = 1;

        public ulong ManagerRole { get; set; }
    }
}
