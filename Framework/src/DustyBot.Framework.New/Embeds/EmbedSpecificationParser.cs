using System;
using Disqord;
using DustyBot.Core.Parsing;
using DustyBot.Framework.Utility;

namespace DustyBot.Framework.Embeds
{
    public static class EmbedSpecificationParser
    {
        public static bool TryParse(string specification, out LocalEmbed embed, out string? error)
        {
            embed = new LocalEmbed();
            error = null;

            var uriValidator = new Func<string?, string, bool>((name, value) => Uri.TryCreate(value, UriKind.Absolute, out _));
            var parts = new[]
            {
                new KeyValueSpecificationPart("title", true, false),
                new KeyValueSpecificationPart("author", true, false),
                new KeyValueSpecificationPart("author link", true, false, "author", validator: uriValidator),
                new KeyValueSpecificationPart("author icon", true, false, "author", validator: uriValidator),
                new KeyValueSpecificationPart("image", true, false, validator: uriValidator),
                new KeyValueSpecificationPart("color", true, false),
                new KeyValueSpecificationPart("thumbnail", true, false, validator: uriValidator),
                new KeyValueSpecificationPart("description", true, true),
                new KeyValueSpecificationPart("footer", true, false),
                new KeyValueSpecificationPart("footer icon", true, false, "footer", validator: uriValidator),
                new KeyValueSpecificationPart("field", false, false, isNameAccepted: true),
                new KeyValueSpecificationPart("inline field", false, false, isNameAccepted: true),
            };

            var parser = new KeyValueSpecificationParser(parts);
            var result = parser.Parse(specification);
            if (!result.Succeeded)
            {
                error = result.Error switch
                {
                    KeyValueSpecificationParser.ErrorType.ValidationFailed => $"The specified {result.ErrorPart.Token} is invalid.",
                    KeyValueSpecificationParser.ErrorType.DuplicatedUniquePart => $"There can only be one {result.ErrorPart.Token}.",
                    KeyValueSpecificationParser.ErrorType.MissingDependency => $"The {result.ErrorPart.DependsOn} must also be specifed with {result.ErrorPart.Token}.",
                    KeyValueSpecificationParser.ErrorType.RequiredPartMissing => $"The {result.ErrorPart.Token} is missing.",
                    _ => "Invalid input"
                };

                return false;
            }

            foreach (var (part, match) in result.Matches)
            {
                switch (part.Token)
                {
                    case "title": embed.WithTitle(match.Value); break;
                    case "author": (embed.Author ??= new LocalEmbedAuthor()).WithName(match.Value); break;
                    case "author link": (embed.Author ??= new LocalEmbedAuthor()).WithUrl(match.Value); break;
                    case "author icon": (embed.Author ??= new LocalEmbedAuthor()).WithIconUrl(match.Value); break;
                    case "image": embed.WithImageUrl(match.Value); break;
                    case "color": embed.WithColor(HexColorParser.Parse(match.Value)); break;
                    case "thumbnail": embed.WithThumbnailUrl(match.Value); break;
                    case "description": embed.WithDescription(match.Value); break;
                    case "footer": (embed.Footer ??= new LocalEmbedFooter()).WithText(match.Value); break;
                    case "footer icon": (embed.Footer ??= new LocalEmbedFooter()).WithIconUrl(match.Value); break;
                    case "field": embed.AddField(match.Name, match.Value, false); break;
                    case "inline field": embed.AddField(match.Name, match.Value, true); break;
                }
            }

            return true;
        }
    }
}
