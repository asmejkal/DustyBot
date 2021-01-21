using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class MassMentionsRule : RaidProtectionRule
    {
        public int MentionsLimit { get; set; }

        public override string ToString()
        {
            return base.ToString() + $"; MentionsLimit={MentionsLimit}";
        }

        protected override void Fill(Dictionary<string, string> pairs)
        {
            base.Fill(pairs);
            MentionsLimit = int.Parse(pairs["MentionsLimit"]);
        }
    }
}
