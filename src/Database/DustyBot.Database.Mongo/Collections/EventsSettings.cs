using System;
using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Settings
{
    public class GreetEmbed
    {
        public string Title { get; set; }
        public Uri Image { get; set; }
        public string Body { get; set; }
        public uint? Color { get; set; }
        public string Footer { get; set; }

        public GreetEmbed() { }

        public GreetEmbed(string title, string body, Uri image = null)
        {
            Title = !string.IsNullOrEmpty(title) ? title : throw new ArgumentException("title");
            Image = image;
            Body = !string.IsNullOrEmpty(body) ? body : throw new ArgumentException("body");
        }
    }

    public class EventsSettings : BaseServerSettings
    {
        public ulong GreetChannel { get; set; }
        public string GreetMessage { get; set; }
        public GreetEmbed GreetEmbed { get; set; }

        public ulong ByeChannel { get; set; }
        public string ByeMessage { get; set; }

        public void ResetGreet()
        {
            GreetChannel = default;
            GreetMessage = default;
            GreetEmbed = default;
        }
    }
}
