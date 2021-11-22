using System;
using System.Collections.Generic;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public class FailureArgumentParserResult : ArgumentParserResult
    {
        public override bool IsSuccessful => false;
        public override string FailureReason => Type switch
        {
            ArgumentParserFailureType.NotEnoughParameters => "One or more required parameters are missing.",
            ArgumentParserFailureType.TooManyParameters => "Incorrect or too many parameters.",
            _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unknown enum value")
        };

        public ArgumentParserFailureType Type { get; }

        public FailureArgumentParserResult(IReadOnlyDictionary<Parameter, object> arguments, ArgumentParserFailureType type)
            : base(arguments)
        {
            Type = type;
        }
    }
}
