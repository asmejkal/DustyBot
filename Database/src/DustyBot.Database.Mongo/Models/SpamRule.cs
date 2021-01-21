using System;
using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
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
}
