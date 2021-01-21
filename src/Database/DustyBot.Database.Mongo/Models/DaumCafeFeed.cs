using System;

namespace DustyBot.Database.Mongo.Models
{
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
}
