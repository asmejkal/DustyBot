using System;
using System.Collections.Generic;
using Qmmands;
using Qmmands.Text;
using Qommon;

namespace DustyBot.Framework.Commands.Parsing
{
    public class FailureArgumentParserResult : IArgumentParserResult
    {
        public bool IsSuccessful => false;
        public string FailureReason => Type switch
        {
            ArgumentParserFailureType.NotEnoughParameters => "One or more required parameters are missing.",
            ArgumentParserFailureType.TooManyParameters => "Incorrect or too many parameters.",
            _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unknown enum value")
        };

        public ArgumentParserFailureType Type { get; }

        public IDictionary<IParameter, object?>? Arguments { get; }

        public IDictionary<IParameter, MultiString>? RawArguments { get; }

        public FailureArgumentParserResult(IDictionary<IParameter, object?> arguments, ArgumentParserFailureType type)
        {
            Arguments = arguments;
            Type = type;
        }
    }
}
