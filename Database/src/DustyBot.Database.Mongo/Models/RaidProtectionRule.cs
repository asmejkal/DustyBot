using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Models
{
    [BsonKnownTypes(typeof(MassMentionsRule), typeof(SpamRule), typeof(PhraseBlacklistRule))]
    public class RaidProtectionRule
    {
        public bool Enabled { get; set; }
        public int MaxOffenseCount { get; set; }
        public TimeSpan OffenseWindow { get; set; }
        public bool Delete { get; set; }
        public RaidProtectionRuleType Type { get; set; }

        public override string ToString()
        {
            return $"Enabled={Enabled}; MaxOffenseCount={MaxOffenseCount}; OffenseWindow={OffenseWindow.TotalSeconds}; Delete={Delete}";
        }

        public void Fill(string s) => Fill(ParseValuePairs(s));

        public static RaidProtectionRule Create(RaidProtectionRuleType type, string s)
        {
            RaidProtectionRule rule;
            switch (type)
            {
                case RaidProtectionRuleType.MassMentionsRule: rule = new MassMentionsRule(); break;
                case RaidProtectionRuleType.TextSpamRule: rule = new SpamRule(); break;
                case RaidProtectionRuleType.ImageSpamRule: rule = new SpamRule(); break;
                case RaidProtectionRuleType.PhraseBlacklistRule: rule = new PhraseBlacklistRule(); break;
                default: throw new ArgumentException("Unknown rule type");
            }

            rule.Type = type;
            rule.Fill(s);
            return rule;
        }

        protected virtual void Fill(Dictionary<string, string> pairs)
        {
            Enabled = bool.Parse(pairs["Enabled"]);
            MaxOffenseCount = int.Parse(pairs["MaxOffenseCount"]);
            OffenseWindow = TimeSpan.FromSeconds(double.Parse(pairs["OffenseWindow"]));
            Delete = bool.Parse(pairs["Delete"]);
        }

        protected static Dictionary<string, string> ParseValuePairs(string s)
        {
            return s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    var pair = x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    return pair.Length >= 2 ? Tuple.Create(pair[0].Trim(), pair[1].Trim()) : null;
                })
                .Where(x => x != null)
                .ToDictionary(x => x.Item1, x => x.Item2);
        }
    }
}
