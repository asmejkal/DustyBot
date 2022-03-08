using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Core.Formatting;
using DustyBot.DaumCafe;
using DustyBot.Framework.Entities;

namespace DustyBot.Service.Services.DaumCafe
{
    internal class DaumCafePostSender : IDaumCafePostSender
    {
        public async Task SendPostAsync(IMessageGuildChannel channel, DaumCafePage post, CancellationToken ct)
        {
            var preview = CreatePreview(post);
            await channel.SendMessageCheckedAsync(preview, cancellationToken: ct);
        }

        private LocalEmbed BuildPreview(string title, Uri url, string? description, string? imageUrl, string cafeName)
        {
            var embed = new LocalEmbed()
                        .WithTitle(title)
                        .WithUrl(url.AbsoluteUri)
                        .WithFooter("Daum Cafe • " + cafeName);

            if (!string.IsNullOrWhiteSpace(description))
                embed.Description = description.JoinWhiteLines(2).TruncateLines(13, trim: true).Truncate(350);

            if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.Contains("cafe_meta_image.png"))
                embed.ImageUrl = imageUrl;

            return embed;
        }

        private LocalMessage CreatePreview(DaumCafePage post)
        {
            var result = new LocalMessage().WithContent($"<{post.DesktopUri.AbsoluteUri}>");
            if (post.Type == DaumCafePageType.Comment && (!string.IsNullOrWhiteSpace(post.Body.Text) || !string.IsNullOrWhiteSpace(post.ImageUrl)))
            {
                result.WithEmbeds(BuildPreview("New memo", post.MobileUri, post.Body.Text, post.Body.ImageUrl, post.CafeId));
            }
            else if (!string.IsNullOrEmpty(post.Body.Subject) && (!string.IsNullOrWhiteSpace(post.Body.Text) || !string.IsNullOrWhiteSpace(post.ImageUrl)))
            {
                result.WithEmbeds(BuildPreview(post.Body.Subject, post.MobileUri, post.Body.Text, post.Body.ImageUrl, post.CafeId));
            }
            else if (post.Type == DaumCafePageType.Article && !string.IsNullOrWhiteSpace(post.Title) && (!string.IsNullOrWhiteSpace(post.Description) || !string.IsNullOrWhiteSpace(post.ImageUrl)))
            {
                result.WithEmbeds(BuildPreview(post.Title, post.MobileUri, post.Description, post.ImageUrl, post.CafeId));
            }

            return result;
        }
    }
}
