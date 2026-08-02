using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord.Bot.Commands;
using DustyBot.Framework.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class MatchTypeParser : DiscordTypeParser<Match>
    {
        public override ValueTask<ITypeParserResult<Match>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            var pattern = parameter.CustomAttributes.OfType<PatternAttribute>().FirstOrDefault();
            if (pattern == null)
                throw new InvalidOperationException("Match parameter must have a Pattern attribute");

            var match = pattern.Regex.Match(value.Span.ToString());
            return match.Success ? Success(match) : Failure("Invalid format.");
        }
    }
}
