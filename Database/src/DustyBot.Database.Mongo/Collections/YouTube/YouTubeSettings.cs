using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Collections.YouTube.Models;

namespace DustyBot.Database.Mongo.Collections.YouTube
{
    public class YouTubeSettings : BaseServerSettings
    {
        public List<YouTubeSong> Songs { get; set; } = new();
    }
}
