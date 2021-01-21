using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class StarboardSettings : BaseServerSettings
    {
        public List<Starboard> Starboards { get; set; } = new List<Starboard>();
        public int NextId { get; set; } = 1;
    }
}
