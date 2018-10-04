using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;
using LiteDB;

namespace DustyBot.Settings
{
    public enum RaidProtectionRuleType
    {
        MassMentionsRule,
        TextSpamRule,
        ImageSpamRule,
        PhraseBlacklistRule
    }

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

    public class RaidProtectionSettings : BaseServerSettings
    {
        public RaidProtectionSettings()
        {
            //Defaults
            Rules.Add(RaidProtectionRuleType.MassMentionsRule, new MassMentionsRule() { Type = RaidProtectionRuleType.MassMentionsRule, Enabled = true, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), MentionsLimit = 10 });
            Rules.Add(RaidProtectionRuleType.TextSpamRule, new SpamRule() { Type = RaidProtectionRuleType.TextSpamRule, Enabled = true, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), Window = TimeSpan.FromSeconds(3), Threshold = 6 });
            Rules.Add(RaidProtectionRuleType.ImageSpamRule, new SpamRule() { Type = RaidProtectionRuleType.ImageSpamRule, Enabled = false, Delete = true, MaxOffenseCount = 2, OffenseWindow = TimeSpan.FromMinutes(5), Window = TimeSpan.FromSeconds(3), Threshold = 6 });
            Rules.Add(RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule() { Type = RaidProtectionRuleType.PhraseBlacklistRule, Enabled = false, Delete = true, MaxOffenseCount = 3, OffenseWindow = TimeSpan.FromMinutes(5) });
        }

        public bool Enabled { get; set; }
        public ulong LogChannel { get; set; }

        public Dictionary<RaidProtectionRuleType, RaidProtectionRule> Rules = new Dictionary<RaidProtectionRuleType, RaidProtectionRule>();

        public MassMentionsRule MassMentionsRule => (MassMentionsRule)Rules[RaidProtectionRuleType.MassMentionsRule];
        public SpamRule TextSpamRule => (SpamRule)Rules[RaidProtectionRuleType.TextSpamRule];
        public SpamRule ImageSpamRule => (SpamRule)Rules[RaidProtectionRuleType.ImageSpamRule];
        public PhraseBlacklistRule PhraseBlacklistRule => (PhraseBlacklistRule)Rules[RaidProtectionRuleType.PhraseBlacklistRule];
    }
}
