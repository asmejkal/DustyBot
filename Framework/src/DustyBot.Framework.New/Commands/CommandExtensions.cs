using System.Collections.Generic;
using System.Linq;
using Disqord.Bot;
using DustyBot.Framework.Commands.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class CommandExtensions
    {
        public static IEnumerable<string> GetExamples(this Command x) =>
            x.Parameters.OfType<ExampleAttribute>().Select(x => x.Example);

        public static bool HideInvocation(this Command x) =>
            x.Attributes.OfType<HideInvocationAttribute>().Any();

        public static bool IsLongRunning(this Command x) =>
            x.Attributes.OfType<LongRunningAttribute>().Any();

        public static bool IsLongRunning(this CommandBuilder x) =>
            x.Attributes.OfType<LongRunningAttribute>().Any();

        public static bool IsHidden(this Command x) =>
            x.Attributes.Any(x => x is HiddenAttribute) || !x.IsEnabled || x.Attributes.Any(x => x is RequireBotOwnerAttribute);
    }
}
