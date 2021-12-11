using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class ReactionsSettings : BaseServerSettings
    {
        public List<Reaction> Reactions { get; set; } = new();
        public int NextReactionId { get; set; } = 1;
        public ulong ManagerRole { get; set; }
        public bool IsPublic { get; set; }

        public void Reset()
        {
            Reactions.Clear();
            NextReactionId = 1;
        }
    }
}
