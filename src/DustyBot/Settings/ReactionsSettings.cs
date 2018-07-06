using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;
using LiteDB;

namespace DustyBot.Settings
{
    public class Reaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Value { get; set; }
    }

    public class ReactionGroup : List<Reaction>
    {
        public string GetRandom()
        {
            if (Count <= 0)
                throw new InvalidOperationException("No reactions found.");

            return this[(new Random().Next(Count))].Value;
        }
    }

    public class ReactionsSettings : BaseServerSettings
    {
        public Dictionary<string, ReactionGroup> Groups { get; set; } = new Dictionary<string, ReactionGroup>();
    }
}
