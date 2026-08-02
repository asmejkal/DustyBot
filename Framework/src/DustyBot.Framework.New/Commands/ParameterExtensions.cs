using System.Linq;
using DustyBot.Framework.Commands.Attributes;
using Qmmands;

namespace DustyBot.Framework.Commands
{
    public static class ParameterExtensions
    {
        public static bool HasDefaultValue(this IParameter x) =>
            !x.GetTypeInformation().IsEnumerable && (x.GetTypeInformation().IsOptional || x.CustomAttributes.Any(x => x is DefaultAttribute) || x.DefaultValue.HasValue);

        public static bool IsHidden(this IParameter x) =>
            x.CustomAttributes.Any(x => x is HiddenAttribute);
    }
}
