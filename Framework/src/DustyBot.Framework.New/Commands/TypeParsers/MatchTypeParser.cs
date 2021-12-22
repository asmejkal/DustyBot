using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord.Bot;
using DustyBot.Framework.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class MatchTypeParser : DiscordTypeParser<Match>
    {
        public override ValueTask<TypeParserResult<Match>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            var pattern = parameter.Attributes.OfType<PatternAttribute>().FirstOrDefault();
            if (pattern == null)
                throw new InvalidOperationException("Match parameter must have a Pattern attribute");

            var match = pattern.Regex.Match(value);
            return match.Success ? Success(match) : Failure("Invalid format.");
        }
    }
}
