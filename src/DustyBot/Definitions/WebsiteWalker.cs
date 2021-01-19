using DustyBot.Configuration;
using Microsoft.Extensions.Options;

namespace DustyBot.Definitions
{
    public class WebsiteWalker
    {
        private const string ReferencePath = "/reference";
        private const string ScheduleGuidePath = "/schedule";
        private const string RolesGuidePath = "/roles";
        private const string ImagesPath = "/img";

        private readonly IOptions<WebOptions> _options;

        public WebsiteWalker(IOptions<WebOptions> options)
        {
            _options = options;
        }

        public string Root => _options.Value.WebsiteRoot;
        public string ReferenceUrl => _options.Value.WebsiteRoot + ReferencePath;
        public string ScheduleGuideUrl => _options.Value.WebsiteRoot + ScheduleGuidePath;
        public string RolesGuideUrl => _options.Value.WebsiteRoot + RolesGuidePath;
        public string ImagesFolderUrl => _options.Value.WebsiteRoot + ImagesPath;
        
        public string LfIconUrl => ImagesFolderUrl + "/lf.png";
        public string SpotifyIconUrl => ImagesFolderUrl + "/sf.png";
        public string InstagramIconUrl => ImagesFolderUrl + "/ig.png";

        public string SpotifyConnectUrl => _options.Value.SpotifyConnectUrl;

        public static string GetModuleWebAnchor(string name) => name.Replace(' ', '-').ToLowerInvariant();
    }
}
