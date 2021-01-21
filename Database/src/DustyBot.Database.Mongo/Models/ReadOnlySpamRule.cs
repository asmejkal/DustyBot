using System;

namespace DustyBot.Database.Mongo.Models
{
    public class ReadOnlySpamRule : ReadOnlyRaidProtectionRule
    {
        private SpamRule Inner { get; }

        public ReadOnlySpamRule(SpamRule inner)
            : base(inner)
        {
            Inner = inner;
        }

        public TimeSpan Window => Inner.Window;
        public int Threshold => Inner.Threshold;
        public override string ToString() => Inner.ToString();
    }
}
