using System.Collections.Generic;
using System.Linq;
using Disqord.Bot.Commands;
using DustyBot.Framework.Commands.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class CommandExtensions
    {
        public static IEnumerable<string> GetExamples(this ICommand x) =>
            x.CustomAttributes.OfType<ExampleAttribute>().Select(x => x.Example);

        public static bool HideInvocation(this ICommand x) =>
            x.CustomAttributes.OfType<HideInvocationAttribute>().Any();

        public static bool IsLongRunning(this ICommand x) =>
            x.CustomAttributes.OfType<LongRunningAttribute>().Any();

        public static bool IsLongRunning(this ICommandBuilder x) =>
            x.CustomAttributes.OfType<LongRunningAttribute>().Any();

        public static bool IsHidden(this ICommand x) =>
            x.CustomAttributes.Any(x => x is HiddenAttribute) || x.CustomAttributes.Any(x => x is RequireBotOwnerAttribute);
    }
}
