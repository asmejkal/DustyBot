using System;
using System.Threading.Tasks;
using Disqord.Bot;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class DateOnlyTypeParser : DiscordTypeParser<DateOnly>
    {
        public override ValueTask<TypeParserResult<DateOnly>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (!DateOnly.TryParseExact(value, new[] { @"yyyy\/M\/d", @"M\/d" }, out var date))
                return Failure("Invalid date format.");

            return Success(date);
        }
    }
}
