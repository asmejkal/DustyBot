using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Http;
using Disqord.Rest;
using DustyBot.Framework.Entities;
using Qmmands;
using Qommon;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class MemberTypeParser : DiscordGuildTypeParser<IMember>
    {
        public override async ValueTask<ITypeParserResult<IMember>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (Snowflake.TryParse(value.Span, out var id) || Mention.TryParseUser(value.Span, out id))
            {
                try
                {
                    var guild = context.Bot.GetGuild(context.GuildId);
                    if (guild is null)
                        return Failure("Guild not found");

                    var result = await guild.GetOrFetchMemberAsync(id);
                    return result != null ? Success(Optional.Create(result)) : Failure("User not found.");
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
