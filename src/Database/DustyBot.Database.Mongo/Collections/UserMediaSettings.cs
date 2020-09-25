using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Settings
{
    public class UserMediaSettings : BaseUserSettings
    {
        public InstagramPreviewStyle InstagramPreviewStyle { get; set; }
        public bool InstagramAutoPreviews { get; set; }
    }
}
