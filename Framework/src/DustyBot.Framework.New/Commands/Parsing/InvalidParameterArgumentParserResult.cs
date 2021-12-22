using System;
using System.Collections.Generic;
using Disqord;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Utility;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public class InvalidParameterArgumentParserResult : ArgumentParserResult
    {
        public int InvalidPosition { get; set; }
        public string InvalidToken { get; }
        public IResult Result { get; }

        public override bool IsSuccessful => false;

        public override string FailureReason => $"Parameter {InvalidPosition} ({_preview}) is invalid. "
            + (Result is FailedResult failedResult ? failedResult.FailureReason : "");

        private readonly string _preview;

        public InvalidParameterArgumentParserResult(IReadOnlyDictionary<Parameter, object?> arguments, int position, string invalidToken, IResult result) 
            : base(arguments)
        {
            InvalidPosition = position;
            InvalidToken = invalidToken ?? throw new ArgumentNullException(nameof(invalidToken));
            Result = result;

            _preview = MessageHelpers.IsMention(invalidToken) ? invalidToken : Markdown.Escape(invalidToken.Truncate(15));
        }
    }
}
