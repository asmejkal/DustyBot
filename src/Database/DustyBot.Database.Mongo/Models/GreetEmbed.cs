using System;

namespace DustyBot.Database.Mongo.Models
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
}
