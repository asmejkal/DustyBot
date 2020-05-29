using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Core.Constants
{
    public class WebConstants
    {
        public const string WebsiteRoot = "http://dustybot.info";
        public const string WebsiteShorthand = "dustybot.info";
        public const string ReferencePath = "/reference";
        public const string ScheduleGuidePath = "/schedule";
        public const string ImagesPath = "/img";

        public const string ReferenceUrl = WebsiteRoot + ReferencePath;
        public const string ScheduleGuideUrl = WebsiteRoot + ScheduleGuidePath;
        public const string ImagesFolderUrl = WebsiteRoot + ImagesPath;
        public const string LfIconUrl = ImagesFolderUrl + "/lf.png";

        public const string SupportServerInvite = "https://discord.gg/mKKJFvZ";

        public static string GetModuleWebAnchor(string moduleName) => moduleName.Replace(' ', '-').ToLowerInvariant();
    }
}
