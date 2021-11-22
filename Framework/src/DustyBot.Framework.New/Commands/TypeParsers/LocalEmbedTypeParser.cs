using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Embeds;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class LocalEmbedTypeParser : DiscordTypeParser<LocalEmbed>
    {
        public override ValueTask<TypeParserResult<LocalEmbed>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (!EmbedSpecificationParser.TryParse(value, out var embed, out var error))
                return Failure(error);

            return Success(embed);
        }
    }
}
