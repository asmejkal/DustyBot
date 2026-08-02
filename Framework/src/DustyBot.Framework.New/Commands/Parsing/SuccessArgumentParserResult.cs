using System.Collections.Generic;
using Qmmands;
using Qmmands.Text;
using Qommon;

namespace DustyBot.Framework.Commands.Parsing
{
    public class SuccessArgumentParserResult : IArgumentParserResult
    {
        public bool IsSuccessful => true;
        public string? FailureReason => null;

        public IDictionary<IParameter, object?>? Arguments { get; }

        public IDictionary<IParameter, MultiString>? RawArguments { get; }

        public SuccessArgumentParserResult(
            IDictionary<IParameter, object?> arguments,
            IDictionary<IParameter, MultiString>? rawArguments = null)
        {
            Arguments = arguments;
            RawArguments = rawArguments;
        }
    }
}
