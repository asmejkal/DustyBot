using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Disqord.Bot;
using DustyBot.Core.Formatting;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class UriTypeParser : DiscordTypeParser<Uri>
    {
        private static readonly HashSet<string> AllowedSchemes = new()
        {
            Uri.UriSchemeHttp,
            Uri.UriSchemeHttps,
            "attachment"
        };

        public override ValueTask<TypeParserResult<Uri>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var result))
                return Failure("Invalid URL.");

            if (!AllowedSchemes.Contains(result.Scheme))
                return Failure($"URL must start with {AllowedSchemes.WordJoinQuotedOr()}.");

            return Success(result);
        }
    }
}
