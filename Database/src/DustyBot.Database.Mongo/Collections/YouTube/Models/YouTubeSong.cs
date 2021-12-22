using System;
using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Collections.YouTube.Models
{
    public class YouTubeSong
    {
        public const string DefaultCategory = "default";

        public string Name { get; set; }
        public HashSet<string> VideoIds { get; set; } = new HashSet<string>();
        public string Category { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public YouTubeSong()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public YouTubeSong(string name, IEnumerable<string> videoIds, string category)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            VideoIds = new HashSet<string>(videoIds ?? throw new ArgumentNullException(nameof(videoIds)));
            Category = category;
        }
    }
}
