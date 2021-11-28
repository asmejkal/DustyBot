using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class RestUserTypeParser : DiscordTypeParser<IRestUser>
    {
        public override async ValueTask<TypeParserResult<IRestUser>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (Snowflake.TryParse(value, out var id) || Mention.TryParseUser(value, out id))
            {
                var result = await context.Bot.FetchUserAsync(id, cancellationToken: context.Bot.StoppingToken).ConfigureAwait(false);
                return result != null ? Success(result) : Failure("User not found.");
            }

            return Failure("Must be a mention or an ID.");
        }
    }
}
