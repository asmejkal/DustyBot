using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Qmmands;

namespace DustyBot.Framework.Commands.TypeParsers
{
    public class GuildTypeParser : DiscordTypeParser<IGuild>
    {
        public override async ValueTask<TypeParserResult<IGuild>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            IGuild? guild;
            if (Snowflake.TryParse(value, out var guildId))
            {
                guild = context.Bot.GetGuild(guildId);
                if (guild == null)
                    guild = await context.Bot.FetchGuildAsync(guildId, cancellationToken: context.Bot.StoppingToken);
            }
            else
            {
                guild = context.Bot.GetGuilds().Values.FirstOrDefault(x => x.Name == value); // Can be unreliable when sharding
            }

            return guild != null ? Success(guild) : Failure("Unknown server.");
        }
    }
}
