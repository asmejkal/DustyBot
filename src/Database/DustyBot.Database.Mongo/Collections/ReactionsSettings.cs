using DustyBot.Database.Mongo.Collections.Templates;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Settings
{
    public class Reaction
    {
        public int Id { get; set; }
        public string Trigger { get; set; }
        public string Value { get; set; }

        public int TriggerCount { get; set; }
    }

    public class ReactionsSettings : BaseServerSettings
    {
        public List<Reaction> Reactions { get; set; } = new List<Reaction>();
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
