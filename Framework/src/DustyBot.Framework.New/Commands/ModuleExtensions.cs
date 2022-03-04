using System.Linq;
using Disqord.Bot;
using DustyBot.Framework.Commands.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class ModuleExtensions
    {
        public static bool IsHidden(this Module x) =>
            x.Attributes.Any(x => x is HiddenAttribute) || !x.IsEnabled || x.Attributes.Any(x => x is RequireBotOwnerAttribute);
    }
}
