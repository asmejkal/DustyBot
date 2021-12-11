using System;
using System.Collections.Generic;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Utility;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public class InvalidParameterArgumentParserResult : ArgumentParserResult
    {
        public int InvalidPosition { get; set; }
        public string InvalidToken { get; }

        public override bool IsSuccessful => false;

        public override string FailureReason => $"Parameter {InvalidPosition} ({_preview}) is invalid.";

        private readonly string _preview;

        public InvalidParameterArgumentParserResult(IReadOnlyDictionary<Parameter, object?> arguments, int position, string invalidToken) 
            : base(arguments)
        {
            InvalidPosition = position;
            InvalidToken = invalidToken ?? throw new ArgumentNullException(nameof(invalidToken));

            _preview = MessageContentHelpers.IsMention(invalidToken) ? invalidToken : invalidToken.Truncate(15);
        }
    }
}
