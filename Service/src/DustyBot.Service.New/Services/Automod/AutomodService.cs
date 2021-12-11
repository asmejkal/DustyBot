using System;
using Disqord;

namespace DustyBot.Service.Services.Automod
{
    internal class AutomodService : IAutomodService
    {
        public int Priority => 0;

        public event EventHandler<(Snowflake GuildId, Snowflake UserId)>? UserAutobanned;
    }
}
