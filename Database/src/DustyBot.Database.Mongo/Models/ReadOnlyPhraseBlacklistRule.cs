using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class ReadOnlyPhraseBlacklistRule : ReadOnlyRaidProtectionRule
    {
        private PhraseBlacklistRule Inner { get; }

        public ReadOnlyPhraseBlacklistRule(PhraseBlacklistRule inner)
            : base(inner)
        {
            Inner = inner;
        }

        public IReadOnlyList<string> Blacklist => Inner.Blacklist;
        public override string ToString() => Inner.ToString();
    }
}
