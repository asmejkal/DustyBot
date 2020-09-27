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
    }

    public class ReactionsSettings : BaseServerSettings
    {
        public List<Reaction> Reactions { get; set; } = new List<Reaction>();
        public int NextReactionId { get; set; } = 1;

        public void Reset()
        {
            Reactions.Clear();
            NextReactionId = 1;
        }

        public string GetRandom(string trigger)
        {
            var reactions = Reactions.Where(x => string.Compare(x.Trigger, trigger, true) == 0).ToList();
            if (reactions.Count <= 0)
                return null;

            return reactions[new Random().Next(reactions.Count)].Value;
        }
    }
}
