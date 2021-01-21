using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class ComebackInfo
    {
        public string Name { get; set; }
        public HashSet<string> VideoIds { get; set; } = new HashSet<string>();
        public string Category { get; set; }
    }
}
