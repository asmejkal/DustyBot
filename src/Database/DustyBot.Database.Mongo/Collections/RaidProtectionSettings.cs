using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Settings
{
    public enum RaidProtectionRuleType
    {
        MassMentionsRule,
        TextSpamRule,
        ImageSpamRule,
        PhraseBlacklistRule
    }

    public class RaidProtectionSettings : BaseServerSettings
    {
        public static readonly IReadOnlyDictionary<RaidProtectionRuleType, RaidProtectionRule> DefaultRules = new Dictionary<RaidProtectionRuleType, RaidProtectionRule>()
        {
            { RaidProtectionRuleType.MassMentionsRule, new MassMentionsRule { Type = RaidProtectionRuleType.MassMentionsRule, Enabled = true, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), MentionsLimit = 10 } },
            { RaidProtectionRuleType.TextSpamRule, new SpamRule { Type = RaidProtectionRuleType.TextSpamRule, Enabled = true, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), Window = TimeSpan.FromSeconds(3), Threshold = 6 } },
            { RaidProtectionRuleType.ImageSpamRule, new SpamRule { Type = RaidProtectionRuleType.ImageSpamRule, Enabled = false, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), Window = TimeSpan.FromSeconds(3), Threshold = 6 } },
            { RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule { Type = RaidProtectionRuleType.PhraseBlacklistRule, Enabled = false, Delete = true, MaxOffenseCount = 3, OffenseWindow = TimeSpan.FromMinutes(5) } }
        };

        public bool Enabled { get; set; }
        public ulong LogChannel { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<RaidProtectionRuleType, RaidProtectionRule> Exceptions { get; set; } = new Dictionary<RaidProtectionRuleType, RaidProtectionRule>();

        [BsonIgnore]
        public MassMentionsRule MassMentionsRule => GetRule<MassMentionsRule>(RaidProtectionRuleType.MassMentionsRule);

        [BsonIgnore]
        public SpamRule TextSpamRule => GetRule<SpamRule>(RaidProtectionRuleType.TextSpamRule);

        [BsonIgnore]
        public SpamRule ImageSpamRule => GetRule<SpamRule>(RaidProtectionRuleType.ImageSpamRule);

        [BsonIgnore]
        public PhraseBlacklistRule PhraseBlacklistRule => GetRule<PhraseBlacklistRule>(RaidProtectionRuleType.PhraseBlacklistRule);

        public T GetRule<T>(RaidProtectionRuleType type)
            where T : RaidProtectionRule => Exceptions.TryGetValue(type, out var rule) ? (T)rule : (T)DefaultRules[type];

        public bool IsDefault(RaidProtectionRuleType type) => !Exceptions.ContainsKey(type);
        public void SetException(RaidProtectionRuleType type, RaidProtectionRule rule) => Exceptions[type] = rule;
        public void ResetException(RaidProtectionRuleType type) => Exceptions.Remove(type);
    }

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
        protected virtual void Fill(Dictionary<string, string> pairs)
        {
            Enabled = bool.Parse(pairs["Enabled"]);
            MaxOffenseCount = int.Parse(pairs["MaxOffenseCount"]);
            OffenseWindow = TimeSpan.FromSeconds(double.Parse(pairs["OffenseWindow"]));
            Delete = bool.Parse(pairs["Delete"]);
        }

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
    
    public class SpamRule : RaidProtectionRule
    {
        public TimeSpan Window { get; set; }
        public int Threshold { get; set; }

        public override string ToString()
        {
            return base.ToString() + $"; Window={Window.TotalSeconds}; Threshold={Threshold}";
        }

        protected override void Fill(Dictionary<string, string> pairs)
        {
            base.Fill(pairs);
            Window = TimeSpan.FromSeconds(double.Parse(pairs["Window"]));
            Threshold = int.Parse(pairs["Threshold"]);
        }
    }

    public class PhraseBlacklistRule : RaidProtectionRule
    {
        public List<string> Blacklist { get; set; } = new List<string>();
    }
}
