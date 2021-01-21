using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Database.Mongo.Models
{
    public class PhraseBlacklistRule : RaidProtectionRule
    {
        public List<string> Blacklist { get; set; } = new List<string>();

        public override string ToString()
        {
            return base.ToString() + $"; Blacklist={string.Join(",", Blacklist)}";
        }

        protected override void Fill(Dictionary<string, string> pairs)
        {
            base.Fill(pairs);
            Blacklist = new List<string>(pairs["Blacklist"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
        }
    }
}
