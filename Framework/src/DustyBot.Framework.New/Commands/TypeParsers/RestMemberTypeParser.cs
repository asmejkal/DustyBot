using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class RestMemberTypeParser : DiscordGuildTypeParser<IMember>
    {
        public override async ValueTask<TypeParserResult<IMember>> ParseAsync(Parameter parameter, string value, DiscordGuildCommandContext context)
        {
            if (Snowflake.TryParse(value, out var id) || Mention.TryParseUser(value, out id))
            {
                var result = await context.Guild.GetOrFetchMemberAsync(id);
                return result != null ? Success(result) : Failure("User not found.");
            }

            return Failure("Must be a mention or an ID.");
        }
    }
}
