using System;
using System.Threading.Tasks;
using Disqord.Bot;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class GuidTypeParser : DiscordTypeParser<Guid>
    {
        public override ValueTask<TypeParserResult<Guid>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (!Guid.TryParse(value, out var result))
                return Failure("Invalid identifier.");

            return Success(result);
        }
    }
}
