using System;
using System.Threading.Tasks;
using Disqord.Bot;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class TimeOnlyTypeParser : DiscordTypeParser<TimeOnly>
    {
        public override ValueTask<TypeParserResult<TimeOnly>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (!TimeOnly.TryParseExact(value, new[] { "HH:mm" }, out var time))
                return Failure("Invalid time format.");

            return Success(time);
        }
    }
}
