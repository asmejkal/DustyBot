using DustyBot.Database.Mongo.Collections.Templates;
using System.Collections.Generic;

namespace DustyBot.Settings
{
    public class Supporter
    {
        public string Name { get; set; }
    }

    public class SupporterSettings : BaseGlobalSettings
    {
        public List<Supporter> Supporters { get; set; } = new List<Supporter>();
    }
}
