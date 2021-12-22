using Disqord;
using Disqord.Bot;
using Qmmands;

namespace DustyBot.Framework.Communication
{
    public interface ICommandUsageBuilder
    {
        LocalEmbed BuildCommandUsageEmbed(Command command, IPrefix commandPrefix);
    }
}
