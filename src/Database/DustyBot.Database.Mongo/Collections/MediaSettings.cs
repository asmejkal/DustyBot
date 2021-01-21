using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class MediaSettings : BaseServerSettings
    {
        public List<ComebackInfo> YouTubeComebacks { get; set; } = new List<ComebackInfo>();
        public List<DaumCafeFeed> DaumCafeFeeds { get; set; } = new List<DaumCafeFeed>();

        public InstagramPreviewStyle InstagramPreviewStyle { get; set; }
        public bool InstagramAutoPreviews { get; set; }
    }
}
