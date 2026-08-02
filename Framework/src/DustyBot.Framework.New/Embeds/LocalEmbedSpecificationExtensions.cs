using System.Text;
using Disqord;

namespace DustyBot.Framework.Embeds
{
    public static class LocalEmbedSpecificationExtensions
    {
        public static string ToSpecification(this LocalEmbed embed)
        {
            var description = new StringBuilder();
            if (embed.Title.HasValue)
                description.AppendLine($"Title: {embed.Title.Value}");

            if (embed.Author.HasValue && embed.Author.Value.Name.HasValue)
            {
                description.AppendLine($"Author: {embed.Author.Value.Name}");

                if (embed.Author.Value.Url.HasValue)
                    description.AppendLine($"Author Link: {embed.Author.Value.Url.Value}");

                if (embed.Author.Value.IconUrl.HasValue)
                    description.AppendLine($"Author Icon: {embed.Author.Value.IconUrl.Value}");
            }

            if (embed.ImageUrl.HasValue)
                description.AppendLine($"Image: {embed.ImageUrl.Value}");

            if (embed.ThumbnailUrl.HasValue)
                description.AppendLine($"Thumbnail: {embed.ThumbnailUrl.Value}");

            if (embed.Color.HasValue)
                description.AppendLine($"Color: {embed.Color.Value}");

            if (embed.Description.HasValue)
                description.AppendLine($"Description: {embed.Description.Value}");

            if (embed.Footer.HasValue && embed.Footer.Value.Text.HasValue)
            {
                description.AppendLine($"Footer: {embed.Footer.Value.Text.Value}");

                if (embed.Footer.Value.IconUrl.HasValue)
                    description.AppendLine($"Footer Icon: {embed.Footer.Value.IconUrl.Value}");
            }

            if (embed.Fields.HasValue)
            {
                foreach (var field in embed.Fields.Value)
                {
                    if (field.IsInline.HasValue && field.IsInline.Value)
                        description.AppendLine($"Inline Field ({field.Name.Value}): {field.Value.Value}");
                    else
                        description.AppendLine($"Field ({field.Name.Value}): {field.Value.Value}");
                }
            }            

            return description.ToString();
        }
    }
}
