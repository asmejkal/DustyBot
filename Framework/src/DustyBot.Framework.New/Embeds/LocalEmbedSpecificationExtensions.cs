using System.Text;
using Disqord;

namespace DustyBot.Framework.Embeds
{
    public static class LocalEmbedSpecificationExtensions
    {
        public static string ToSpecification(this LocalEmbed embed)
        {
            var description = new StringBuilder();
            if (!string.IsNullOrEmpty(embed.Title))
                description.AppendLine($"Title: {embed.Title}");

            if (embed.Author != null && !string.IsNullOrEmpty(embed.Author.Name))
            {
                description.AppendLine($"Author: {embed.Author.Name}");

                if (!string.IsNullOrEmpty(embed.Author.Url))
                    description.AppendLine($"Author Link: {embed.Author.Url}");

                if (!string.IsNullOrEmpty(embed.Author.IconUrl))
                    description.AppendLine($"Author Icon: {embed.Author.IconUrl}");
            }

            if (!string.IsNullOrEmpty(embed.ImageUrl))
                description.AppendLine($"Image: {embed.ImageUrl}");

            if (!string.IsNullOrEmpty(embed.ThumbnailUrl))
                description.AppendLine($"Thumbnail: {embed.ThumbnailUrl}");

            if (embed.Color.HasValue)
                description.AppendLine($"Color: {embed.Color.Value}");

            description.AppendLine($"Description: {embed.Description}");

            if (embed.Footer != null && !string.IsNullOrEmpty(embed.Footer.Text))
            {
                description.AppendLine($"Footer: {embed.Footer.Text}");

                if (!string.IsNullOrEmpty(embed.Footer.IconUrl))
                    description.AppendLine($"Footer Icon: {embed.Footer.IconUrl}");
            }

            foreach (var field in embed.Fields)
            {
                if (field.IsInline)
                    description.AppendLine($"Inline Field ({field.Name}): {field.Value}");
                else
                    description.AppendLine($"Field ({field.Name}): {field.Value}");
            }

            return description.ToString();
        }
    }
}
