using System.Linq;
using DustyBot.Framework.Commands.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class ParameterExtensions
    {
        public static bool HasDefaultValue(this Parameter x) =>
            !x.IsMultiple && (x.IsOptional || x.Attributes.Any(x => x is DefaultAttribute) || x.DefaultValue != default);

        public static bool IsHidden(this Parameter x) =>
            x.Attributes.Any(x => x is HiddenAttribute);
    }
}
