using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Http;
using Disqord.Rest;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class MemberTypeParser : DiscordGuildTypeParser<IMember>
    {
        public override async ValueTask<TypeParserResult<IMember>> ParseAsync(Parameter parameter, string value, DiscordGuildCommandContext context)
        {
            if (Snowflake.TryParse(value, out var id) || Mention.TryParseUser(value, out id))
            {
                try
                {
                    var result = await context.Guild.GetOrFetchMemberAsync(id);
                    return result != null ? Success(result) : Failure("User not found.");
                }
                catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound && ex.IsError(RestApiErrorCode.UnknownUser))
                {
                    return Failure("Unknown user.");
                }
            }

            return Failure("Must be a mention or an ID.");
        }
    }
}
