using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Collections
{
    public class BotSettings : BaseServerSettings
    {
        public string? CommandPrefix { get; set; }
    }
}
