namespace DustyBot.DaumCafe
{
    public class DaumCafePage
    {
        public string CafeId { get; }
        public string BoardId { get; }
        public int PostId { get; }

        public DaumCafePageType? Type { get; }
        public string? Title { get; }
        public string? ImageUrl { get; }
        public string? Description { get; }

        public DaumCafePageBody Body { get; }

        public Uri MobileUri { get; }
        public Uri DesktopUri { get; }

        public DaumCafePage(
            string cafeId,
            string boardId,
            int postId,
            DaumCafePageType? type,
            DaumCafePageBody body,
            Uri mobileUri,
            Uri desktopUri,
            string? title = null,
            string? imageUrl = null,
            string? description = null)
        {
            CafeId = cafeId ?? throw new ArgumentNullException(nameof(cafeId));
            BoardId = boardId ?? throw new ArgumentNullException(nameof(boardId));
            PostId = postId;
            Type = type;
            Title = title;
            ImageUrl = imageUrl;
            Description = description;
            Body = body ?? throw new ArgumentNullException(nameof(body));
            MobileUri = mobileUri ?? throw new ArgumentNullException(nameof(mobileUri));
            DesktopUri = desktopUri ?? throw new ArgumentNullException(nameof(desktopUri));
        }
    }
}
