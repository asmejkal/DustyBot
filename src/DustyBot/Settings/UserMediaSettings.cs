using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings
{
    public class UserMediaSettings : BaseUserSettings
    {
        public InstagramPreviewStyle InstagramPreviewStyle { get; set; }
        public bool InstagramAutoPreviews { get; set; }
    }
}
