using System;

namespace DustyBot.Database.Mongo.Models
{
    public class ReadOnlyRaidProtectionRule
    {
        private RaidProtectionRule Inner { get; }

        public ReadOnlyRaidProtectionRule(RaidProtectionRule inner)
        {
            Inner = inner;
        }

        public bool Enabled => Inner.Enabled;
        public int MaxOffenseCount => Inner.MaxOffenseCount;
        public TimeSpan OffenseWindow => Inner.OffenseWindow;
        public bool Delete => Inner.Delete;
        public RaidProtectionRuleType Type => Inner.Type;
        public override string ToString() => Inner.ToString();
    }
}
