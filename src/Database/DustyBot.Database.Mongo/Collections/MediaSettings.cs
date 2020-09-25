using DustyBot.Database.Mongo.Collections.Templates;
using System;
using System.Collections.Generic;

namespace DustyBot.Settings
{
    public enum InstagramPreviewStyle
    {
        None,
        Embed,
        Text
    }

    public class ComebackInfo
    {
        public string Name { get; set; }
        public HashSet<string> VideoIds { get; set; } = new HashSet<string>();
        public string Category { get; set; }
    }

    public class DaumCafeFeed
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string CafeId { get; set; }
        public string BoardId { get; set; }
        public int LastPostId { get; set; } = -1;
        public ulong TargetChannel { get; set; }

        public ulong CredentialUser { get; set; }
        public Guid CredentialId { get; set; }
    }

    public class MediaSettings : BaseServerSettings
    {
        public List<ComebackInfo> YouTubeComebacks { get; set; } = new List<ComebackInfo>();
        public List<DaumCafeFeed> DaumCafeFeeds { get; set; } = new List<DaumCafeFeed>();

        public InstagramPreviewStyle InstagramPreviewStyle { get; set; }
        public bool InstagramAutoPreviews { get; set; }
    }
}
