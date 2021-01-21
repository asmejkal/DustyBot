using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class SupporterSettings : BaseGlobalSettings
    {
        public List<Supporter> Supporters { get; set; } = new List<Supporter>();
    }
}
