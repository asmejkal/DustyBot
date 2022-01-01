using System;
using Disqord.Bot;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyModuleBase : DustyModuleBase<DiscordCommandContext>
    {
        protected bool HasGuildContext => Context is DiscordGuildCommandContext;

        protected DiscordGuildCommandContext GuildContext => Context switch
        {
            DiscordGuildCommandContext x => x,
            _ => throw new InvalidOperationException("Invalid context")
        };
    }
}
