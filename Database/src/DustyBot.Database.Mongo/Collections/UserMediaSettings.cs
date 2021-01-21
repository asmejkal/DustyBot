using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class UserMediaSettings : BaseUserSettings
    {
        public InstagramPreviewStyle InstagramPreviewStyle { get; set; }
        public bool InstagramAutoPreviews { get; set; }
    }
}
