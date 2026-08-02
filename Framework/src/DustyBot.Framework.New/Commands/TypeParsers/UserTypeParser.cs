using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Qmmands;
using Qommon;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class UserTypeParser : DiscordTypeParser<IUser>
    {
        public override async ValueTask<ITypeParserResult<IUser>> ParseAsync(IDiscordCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (Snowflake.TryParse(value.Span, out var id) || Mention.TryParseUser(value.Span, out id))
            {
                var result = (IUser?)context.Bot.GetUser(id)
                    ?? await context.Bot.FetchUserAsync(id, cancellationToken: context.Bot.StoppingToken);

                return result != null ? Success(Optional.Create(result)) : Failure("User not found.");
            }

            return Failure("Must be a mention or an ID.");
        }
    }
}
