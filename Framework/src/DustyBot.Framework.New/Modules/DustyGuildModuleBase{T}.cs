using Disqord.Bot;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyGuildModuleBase<T> : DustyModuleBase<T>
        where T : DiscordGuildCommandContext
    {
    }
}
