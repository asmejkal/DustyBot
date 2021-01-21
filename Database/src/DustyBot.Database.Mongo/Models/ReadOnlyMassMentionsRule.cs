namespace DustyBot.Database.Mongo.Models
{
    public class ReadOnlyMassMentionsRule : ReadOnlyRaidProtectionRule
    {
        private MassMentionsRule Inner { get; }

        public ReadOnlyMassMentionsRule(MassMentionsRule inner)
            : base(inner)
        {
            Inner = inner;
        }

        public int MentionsLimit => Inner.MentionsLimit;
        public override string ToString() => Inner.ToString();
    }
}
