using Discord;
using DustyBot.Framework.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DustyBot.Core.Formatting;

namespace DustyBot.Helpers
{
    static class PrintHelpers
    {
        public class Thumbnail
        {
            public string Url { get; set; }
            public bool IsVideo { get; set; }
            public string VideoUrl { get; set; }

            public Thumbnail(string url, bool isVideo = false, string videoUrl = null)
            {
                Url = url;
                IsVideo = isVideo;
                VideoUrl = videoUrl;
            }
        }

        public const int MediaPerTextMessage = 5;
        public const int EmbedMediaCutoff = 9;

        public static EmbedBuilder BuildMediaEmbed(
            string title, 
            IEnumerable<string> media, 
            string url = null, 
            string caption = null,
            Thumbnail thumbnail = null,
            string footer = null, 
            string captionFooter = null,
            DateTimeOffset? timestamp = null, 
            string iconUrl = null,
            int maxCaptionLength = 400,
            int maxCaptionLines = 10)
        {
            var author = new EmbedAuthorBuilder()
                .WithName(title);

            if (!string.IsNullOrEmpty(url))
                author.WithUrl(url);

            if (!string.IsNullOrEmpty(iconUrl))
                author.WithIconUrl(iconUrl);

            var embed = new EmbedBuilder()
                .WithAuthor(author)
                .WithFooter(footer);

            if (timestamp != null)
                embed.WithTimestamp(timestamp.Value);

            var buttons = "";
            const int minCaptionSpace = 100;
            var maxButtonsLength = EmbedBuilder.MaxDescriptionLength - minCaptionSpace - (captionFooter?.Length ?? 0);
            if (media.Skip(1).Any())
            {
                // Add only as many buttons as will fit
                var builder = new StringBuilder();
                foreach (var button in media.Take(Math.Min(EmbedMediaCutoff, 9)).Select((y, i) => $"[{(i + 1).ToKeycapEmoji()}]({y})"))
                {
                    var item = button + " ";
                    if (builder.Length + item.Length > maxButtonsLength)
                        break;

                    builder.Append(item);
                }

                buttons = builder.ToString();
            }
            else if (thumbnail?.IsVideo ?? false)
            {
                buttons = $"[▶️ Play]({thumbnail.VideoUrl})";
            }

            var description = new StringBuilder();
            const int lineBreaksBuffer = 5;
            var captionSpace = EmbedBuilder.MaxDescriptionLength - buttons.Length - (captionFooter?.Length ?? 0) - lineBreaksBuffer;
            if (!string.IsNullOrEmpty(caption))
                description.Append(caption.Truncate(Math.Min(maxCaptionLength, captionSpace)).TruncateLines(maxCaptionLines, trim: true) + "\n\n");

            if (!string.IsNullOrEmpty(captionFooter))
                description.AppendLine(captionFooter);

            description.Append(buttons);

            embed.WithDescription(description.ToString());

            if (thumbnail != null)
                embed.WithImageUrl(thumbnail.Url);

            return embed;
        }

        public static IEnumerable<string> BuildMediaText(string title, IEnumerable<string> media, string url = null, string caption = null, string footer = null)
        {
            var result = new StringBuilder();
            result.AppendLine(title);
            if (!string.IsNullOrEmpty(url))
                result.AppendLine($"<{url}>");

            var messages = new List<string>();
            bool first = true;
            do
            {
                var links = new StringBuilder();
                foreach (var item in media.Take(MediaPerTextMessage))
                    links.AppendLine(item);

                if (first && !string.IsNullOrWhiteSpace(caption))
                    result.AppendLine(caption.Truncate(DiscordHelpers.MaxMessageLength - links.Length - result.Length - (footer?.Length ?? 0) - 50).Quote());

                result.Append(links);
                if (first && !string.IsNullOrEmpty(footer))
                    result.AppendLine(footer);

                first = false;
                messages.Add(result.ToString());
                result.Clear();
                media = media.Skip(MediaPerTextMessage);
            } while (media.Any());

            return messages;
        }
    }
}
