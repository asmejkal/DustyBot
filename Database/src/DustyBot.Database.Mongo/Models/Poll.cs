using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Models
{
    public class Poll
    {
        public ulong Channel { get; set; }
        public string Question { get; set; }
        public List<string> Answers { get; set; } = new List<string>();

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
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
}
