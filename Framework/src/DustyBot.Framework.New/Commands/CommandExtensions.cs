using System.Collections.Generic;
using System.Linq;
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
    }
}
