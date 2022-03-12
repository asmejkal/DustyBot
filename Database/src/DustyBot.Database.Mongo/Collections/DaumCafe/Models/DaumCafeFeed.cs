using System;

namespace DustyBot.Database.Mongo.Collections.DaumCafe.Models
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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public DaumCafeFeed()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public DaumCafeFeed(string cafeId, string boardId, ulong targetChannel, ulong credentialUser, Guid credentialId = default)
        {
            CafeId = cafeId ?? throw new ArgumentNullException(nameof(cafeId));
            BoardId = boardId ?? throw new ArgumentNullException(nameof(boardId));
            TargetChannel = targetChannel;
            CredentialUser = credentialUser;
            CredentialId = credentialId;
        }
    }
}
