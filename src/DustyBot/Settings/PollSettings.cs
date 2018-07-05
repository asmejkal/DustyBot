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
    public class Poll
    {
        public ulong Channel { get; set; }
        public string Question { get; set; }
        public List<string> Answers { get; set; } = new List<string>();
        public Dictionary<ulong, int> Votes { get; set; } = new Dictionary<ulong, int>();

        public bool Anonymous { get; set; }

        [BsonIgnore]
        public Dictionary<int, int> Results
        {
            get
            {
                var result = new Dictionary<int, int>();
                for (int i = 1; i <= Answers.Count; ++i)
                    result[i] = 0;

                foreach (var vote in Votes)
                    result[vote.Value]++;

                return result;
            }
        }
    }

    public class PollSettings : BaseServerSettings
    {
        public List<Poll> Polls { get; set; } = new List<Poll>();
    }
}
