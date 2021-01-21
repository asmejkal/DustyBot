using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class StarredMessage
    {
        public int StarCount { get; set; }
        public ulong Author { get; set; }
        public ulong StarboardMessage { get; set; }

        public List<string> Attachments { get; set; }
    }
}
