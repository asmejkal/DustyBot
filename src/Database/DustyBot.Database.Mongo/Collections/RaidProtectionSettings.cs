using System;
using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Collections
{
    public class RaidProtectionSettings : BaseServerSettings
    {
        public static readonly IReadOnlyDictionary<RaidProtectionRuleType, ReadOnlyRaidProtectionRule> DefaultRules = new Dictionary<RaidProtectionRuleType, ReadOnlyRaidProtectionRule>()
        {
            { RaidProtectionRuleType.MassMentionsRule, new MassMentionsRule { Type = RaidProtectionRuleType.MassMentionsRule, Enabled = true, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), MentionsLimit = 10 }.AsReadOnly() },
            { RaidProtectionRuleType.TextSpamRule, new SpamRule { Type = RaidProtectionRuleType.TextSpamRule, Enabled = true, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), Window = TimeSpan.FromSeconds(3), Threshold = 6 }.AsReadOnly() },
            { RaidProtectionRuleType.ImageSpamRule, new SpamRule { Type = RaidProtectionRuleType.ImageSpamRule, Enabled = false, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), Window = TimeSpan.FromSeconds(3), Threshold = 6 }.AsReadOnly() },
            { RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule { Type = RaidProtectionRuleType.PhraseBlacklistRule, Enabled = false, Delete = true, MaxOffenseCount = 3, OffenseWindow = TimeSpan.FromMinutes(5) }.AsReadOnly() }
        };

        public bool Enabled { get; set; }
        public ulong LogChannel { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<RaidProtectionRuleType, RaidProtectionRule> Exceptions { get; set; } = new Dictionary<RaidProtectionRuleType, RaidProtectionRule>();

        [BsonIgnore]
        public ReadOnlyMassMentionsRule MassMentionsRule => GetRule<ReadOnlyMassMentionsRule>(RaidProtectionRuleType.MassMentionsRule);

        [BsonIgnore]
        public ReadOnlySpamRule TextSpamRule => GetRule<ReadOnlySpamRule>(RaidProtectionRuleType.TextSpamRule);

        [BsonIgnore]
        public ReadOnlySpamRule ImageSpamRule => GetRule<ReadOnlySpamRule>(RaidProtectionRuleType.ImageSpamRule);

        [BsonIgnore]
        public ReadOnlyPhraseBlacklistRule PhraseBlacklistRule => GetRule<ReadOnlyPhraseBlacklistRule>(RaidProtectionRuleType.PhraseBlacklistRule);

        public T GetRule<T>(RaidProtectionRuleType type)
            where T : ReadOnlyRaidProtectionRule => Exceptions.TryGetValue(type, out var rule) ? (T)rule.AsReadOnly() : (T)DefaultRules[type];

        public bool IsDefault(RaidProtectionRuleType type) => !Exceptions.ContainsKey(type);
        public void SetException(RaidProtectionRuleType type, RaidProtectionRule rule) => Exceptions[type] = rule;
        public void ResetException(RaidProtectionRuleType type) => Exceptions.Remove(type);
    }
}
