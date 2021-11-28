using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class EventsSettings : BaseServerSettings
    {
        public ulong GreetChannel { get; set; }
        public string GreetMessage { get; set; }
        public GreetByeEmbed GreetEmbed { get; set; }

        public ulong ByeChannel { get; set; }
        public string ByeMessage { get; set; }
        public GreetByeEmbed ByeEmbed { get; set; }

        public void ResetGreet()
        {
            GreetChannel = default;
            GreetMessage = default;
            GreetEmbed = default;
        }

        public void ResetBye()
        {
            ByeChannel = default;
            ByeMessage = default;
            ByeEmbed = default;
        }
    }
}
