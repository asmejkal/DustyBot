using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Qmmands;

namespace DustyBot.Framework.Commands.Attributes
{
    public class InverseRegexAttribute : ParameterCheckAttribute
    {
        public Regex Regex { get; }

        public InverseRegexAttribute(string pattern)
            : this(pattern, RegexOptions.Compiled)
        { 
        }

        public InverseRegexAttribute(string pattern, RegexOptions options)
        {
            Regex = new Regex(pattern, options);
        }

        public override bool CanCheck(IParameter parameter, object? value)
            => value is string;

        public override ValueTask<IResult> CheckAsync(ICommandContext context, IParameter parameter, object? value)
        {
            if (Regex.IsMatch((string)value!))
                return new(Qmmands.Results.Failure("The parameter doesn't match the expected format."));

            return new(Qmmands.Results.Success);
        }
    }
}
