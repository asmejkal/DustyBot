using System.Collections.Generic;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public class SuccessArgumentParserResult : ArgumentParserResult
    {
        public override bool IsSuccessful => true;
        public override string? FailureReason => null;

        public SuccessArgumentParserResult(IReadOnlyDictionary<Parameter, object?> arguments)
            : base(arguments)
        {
        }
    }
}
