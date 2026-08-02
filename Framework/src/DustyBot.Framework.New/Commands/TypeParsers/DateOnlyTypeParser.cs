using System;
using System.Threading.Tasks;
using Disqord.Bot.Commands;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class DateOnlyTypeParser : DiscordTypeParser<DateOnly>
    {
        public override ValueTask<ITypeParserResult<DateOnly>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (!DateOnly.TryParseExact(value.Span, new[] { @"yyyy\/M\/d", @"M\/d" }, out var date))
                return Failure("Invalid date format.");

            return Success(date);
        }
    }
}
