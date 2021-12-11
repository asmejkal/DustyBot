using System;
using Disqord;

namespace DustyBot.Service.Services.Automod
{
    public interface IAutomodService
    {
        int Priority { get; }

        event EventHandler<(Snowflake GuildId, Snowflake UserId)> UserAutobanned;
    }
}
