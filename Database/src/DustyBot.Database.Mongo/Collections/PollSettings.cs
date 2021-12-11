using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class PollSettings : BaseServerSettings
    {
        public List<Poll> Polls { get; set; } = new();
    }
}
