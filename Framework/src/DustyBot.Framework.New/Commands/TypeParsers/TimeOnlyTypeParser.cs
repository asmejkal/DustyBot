using System;
using System.Threading.Tasks;
using Disqord.Bot.Commands;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class TimeOnlyTypeParser : DiscordTypeParser<TimeOnly>
    {
        public override ValueTask<ITypeParserResult<TimeOnly>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (!TimeOnly.TryParseExact(value.Span, new[] { @"HH\:mm" }, out var time))
                return Failure("Invalid time format.");

            return Success(time);
        }
    }
}
