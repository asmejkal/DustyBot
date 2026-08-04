using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class PhraseBlacklistRule : RaidProtectionRule
    {
        public List<string> Blacklist { get; set; } = new List<string>();

        public override RaidProtectionRule Clone()
        {
            var result = Clone<PhraseBlacklistRule>();
            result.Blacklist = new List<string>(Blacklist);
            return result;
        }
    }
}
