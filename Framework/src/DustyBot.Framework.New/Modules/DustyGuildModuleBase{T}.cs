using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyGuildModuleBase<T> : DustyModuleBase<T>
        where T : DustyGuildCommandContext
    {
    }
}
