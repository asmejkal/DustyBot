using System;
using System.Threading.Tasks;
using Disqord.Bot.Commands;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class GuidTypeParser : DiscordTypeParser<Guid>
    {
        public override ValueTask<ITypeParserResult<Guid>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (!Guid.TryParse(value.Span, out var result))
                return Failure("Invalid identifier.");

            return Success(result);
        }
    }
}
