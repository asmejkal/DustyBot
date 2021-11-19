using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class PhraseBlacklistRule : RaidProtectionRule
    {
        public List<string> Blacklist { get; set; } = new List<string>();
    }
}
