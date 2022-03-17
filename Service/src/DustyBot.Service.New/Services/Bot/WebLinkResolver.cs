using System;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Services.Bot
{
    public class WebLinkResolver
    {
        private const string ReferencePath = "/reference";
        private const string ScheduleGuidePath = "/schedule";
        private const string RolesGuidePath = "/roles";
        private const string ImagesPath = "/img";

        public Uri Root => new(_options.WebsiteRoot ?? throw new InvalidOperationException("Web options not configured"));
        public Uri Reference => new(Root, ReferencePath);
        public Uri ScheduleGuide => new(Root, ScheduleGuidePath);
        public Uri RolesGuide => new(Root, RolesGuidePath);
        public Uri ImagesFolder => new(Root, ImagesPath);
        
        public Uri LastFmIcon => new(Root, ImagesPath + "/lf.png");
        public Uri YouTubeIcon => new(Root, ImagesPath + "/yt2.png");
        public Uri SpotifyIcon => new(Root, ImagesPath + "/sf.png");
        public Uri InstagramIcon => new(Root, ImagesPath + "/ig.png");

        public Uri SpotifyConnect => new(_options.SpotifyConnectUrl ?? throw new InvalidOperationException("Web options not configured"));

        private readonly WebOptions _options;

        public WebLinkResolver(IOptions<WebOptions> options)
        {
            _options = options.Value;
        }

        public Uri GetModuleReference(string name) =>
            new UriBuilder(Reference) { Fragment = GetModuleWebAnchor(name) }.Uri;

        public static string GetModuleWebAnchor(string name) => name.Replace(' ', '-').ToLowerInvariant();
    }
}
