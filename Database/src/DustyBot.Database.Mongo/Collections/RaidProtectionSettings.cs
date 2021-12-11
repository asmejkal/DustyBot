using System;
using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Collections
{
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

        public Dictionary<RaidProtectionRuleType, RaidProtectionRule> Exceptions { get; set; } = new();

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
}
