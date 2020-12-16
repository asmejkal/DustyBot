namespace DustyBot.Definitions
{
    public class WebConstants
    {
        public const string WebsiteRoot = "https://dustybot.info";
        public const string WebsiteShorthand = "dustybot.info";
        public const string ReferencePath = "/reference";
        public const string ScheduleGuidePath = "/schedule";
        public const string RolesGuidePath = "/roles";
        public const string ImagesPath = "/img";

        public const string ReferenceUrl = WebsiteRoot + ReferencePath;
        public const string ScheduleGuideUrl = WebsiteRoot + ScheduleGuidePath;
        public const string RolesGuideUrl = WebsiteRoot + RolesGuidePath;
        public const string ImagesFolderUrl = WebsiteRoot + ImagesPath;
        
        public const string LfIconUrl = ImagesFolderUrl + "/lf.png";
        public const string SpotifyIconUrl = ImagesFolderUrl + "/sf.png";
        public const string InstagramIconUrl = ImagesFolderUrl + "/ig.png";

        public const string SupportServerInvite = "https://discord.gg/mKKJFvZ";

        public const string SpotifyConnectUrl = "https://connect.dustybot.info/spotify";

        public static string GetModuleWebAnchor(string moduleName) => moduleName.Replace(' ', '-').ToLowerInvariant();
    }
}
