using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Settings
{
    public class BotSettings : BaseServerSettings
    {
        public string CommandPrefix { get; set; }
    }
}
