using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using DustyBot.Framework.Embeds;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class LocalEmbedTypeParser : DiscordTypeParser<LocalEmbed>
    {
        public override ValueTask<ITypeParserResult<LocalEmbed>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (!EmbedSpecificationParser.TryParse(value.Span.ToString(), out var embed, out var error))
                return Failure(error);

            return Success(embed);
        }
    }
}
