using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Rest;
using Qmmands;
using Qommon;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class RestUserTypeParser : DiscordTypeParser<IRestUser>
    {
        public override async ValueTask<ITypeParserResult<IRestUser>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (Snowflake.TryParse(value.Span, out var id) || Mention.TryParseUser(value.Span, out id))
            {
                var result = await context.Bot.FetchUserAsync(id, cancellationToken: context.Bot.StoppingToken).ConfigureAwait(false);
                return result is not null ? Success(Optional.Create(result)) : Failure("User not found.");
            }

            return Failure("Must be a mention or an ID.");
        }
    }
}
