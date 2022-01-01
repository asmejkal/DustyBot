using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class UserTypeParser : DiscordTypeParser<IUser>
    {
        public override async ValueTask<TypeParserResult<IUser>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            if (Snowflake.TryParse(value, out var id) || Mention.TryParseUser(value, out id))
            {
                var result = (IUser)context.Bot.GetUser(id) 
                    ?? await context.Bot.FetchUserAsync(id, cancellationToken: context.Bot.StoppingToken);

                return result != null ? Success(result) : Failure("User not found.");
            }

            return Failure("Must be a mention or an ID.");
        }
    }
}
