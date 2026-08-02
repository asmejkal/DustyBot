using System.Linq;
using Disqord.Bot.Commands;
using DustyBot.Framework.Commands.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class ModuleExtensions
    {
        public static bool IsHidden(this IModule x) =>
            x.CustomAttributes.Any(x => x is HiddenAttribute) || x.CustomAttributes.Any(x => x is RequireBotOwnerAttribute);
    }
}
