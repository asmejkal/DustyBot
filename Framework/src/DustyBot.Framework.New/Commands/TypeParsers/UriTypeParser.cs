using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord.Bot.Commands;
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

        public override ValueTask<ITypeParserResult<Uri>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            var stringValue = value.Span.ToString();
            if (value.Length > 2 && stringValue.First() == '<' && stringValue.Last() == '>')
                stringValue = stringValue[1..^1];
            
            if (!Uri.TryCreate(stringValue, UriKind.Absolute, out var result))
                return Failure("Invalid URL.");

            if (!AllowedSchemes.Contains(result.Scheme))
                return Failure($"URL must start with {AllowedSchemes.WordJoinQuotedOr()}.");

            return Success(result);
        }
    }
}
