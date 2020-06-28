using Discord;
using DustyBot.Framework.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    static class PrintHelpers
    {
        public class Media
        {
            public bool IsVideo { get; }
            public string Url { get; }
            public string ThumbnailUrl { get; }

            public Media(bool isVideo, string url, string thumbnailUrl)
            {
                IsVideo = isVideo;
                Url = url;
                ThumbnailUrl = thumbnailUrl;
            }
        }

        public static async Task<EmbedBuilder> BuildMediaEmbed(string title, IEnumerable<Media> media, string url, string shortenerKey, string caption = null, string footer = null, DateTimeOffset? timestamp = null, string iconUrl = null)
        {
            var author = new EmbedAuthorBuilder()
                .WithName(title)
                .WithUrl(url);

            if (!string.IsNullOrEmpty(iconUrl))
                author.WithIconUrl(iconUrl);

            var embed = new EmbedBuilder()
                .WithAuthor(author)
                .WithFooter(footer);

            if (timestamp != null)
                embed.WithTimestamp(timestamp.Value);

            string BuildButtons(IEnumerable<string> urls) =>
                string.Join(" ", urls.Take(9).Select((x, i) => $"[{(i + 1).ToKeycapEmoji()}]({x})"));

            var buttons = "";
            if (media.Skip(1).Any())
            {
                buttons = BuildButtons(media.Select(x => x.Url));
                if (buttons.Length > EmbedBuilder.MaxDescriptionLength)
                    buttons = BuildButtons(await Task.WhenAll(media.Select(x => UrlShortener.ShortenUrl(x.Url, shortenerKey))));
            }
            else if (media.Any(x => x.IsVideo))
            {
                buttons = $"[⏯ Play]({media.First().Url})";
            }

            var description = new StringBuilder();
            if (!string.IsNullOrEmpty(caption))
                description.Append(caption.Truncate(EmbedBuilder.MaxDescriptionLength - buttons.Length - 2) + "\n\n");

            description.Append(buttons);

            embed.WithDescription(description.ToString());

            if (media.Any())
            {
                var thumbnail = media.First();
                embed.WithImageUrl(thumbnail.IsVideo ? thumbnail.ThumbnailUrl : thumbnail.Url);
            }

            return embed;
        }

        public static async Task<IEnumerable<string>> BuildMediaText(string title, IEnumerable<Media> media, string shortenerKey, string url = null, string caption = null, string footer = null)
        {
            const int batchSize = 5;

            var result = new StringBuilder();
            result.AppendLine(title);
            if (!string.IsNullOrEmpty(url))
                result.AppendLine($"<{url}>");

            if (!string.IsNullOrWhiteSpace(caption))
                result.AppendLine(caption.Quote());

            var messages = new List<string>();
            bool first = true;
            do
            {
                foreach (var item in media.Take(batchSize))
                    result.AppendLine(item.IsVideo ? item.Url : await UrlShortener.ShortenUrl(item.Url, shortenerKey));

                if (first && !string.IsNullOrEmpty(footer))
                    result.AppendLine(footer);

                first = false;
                messages.Add(result.ToString());
                result.Clear();
                media = media.Skip(batchSize);
            } while (media.Any());

            return messages;
        }
    }
}
