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

        public override ValueTask<CheckResult> CheckAsync(object argument, CommandContext context)
        {
            if (Regex.IsMatch((string)argument))
                return Failure($"The parameter doesn't match the expected format.");

            return Success();
        }
    }
}
