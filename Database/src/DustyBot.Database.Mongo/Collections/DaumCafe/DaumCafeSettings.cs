using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.DaumCafe.Models;
using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Collections.DaumCafe
{
    public class DaumCafeSettings : BaseServerSettings
    {
        public List<DaumCafeFeed> Feeds { get; set; } = new();
    }
}
