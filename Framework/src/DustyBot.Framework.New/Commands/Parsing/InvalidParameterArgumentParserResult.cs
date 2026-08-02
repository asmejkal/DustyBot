using System;
using System.Collections.Generic;
using Disqord;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Utility;
using Qmmands;
using Qmmands.Text;
using Qommon;

namespace DustyBot.Framework.Commands.Parsing
{
    public class InvalidParameterArgumentParserResult : IArgumentParserResult
    {
        public int InvalidPosition { get; set; }
        public string InvalidToken { get; }
        public IResult Result { get; }

        public bool IsSuccessful => false;

        public string FailureReason => $"Parameter {InvalidPosition} ({_preview}) is invalid. "
            + (Result is FailedResult failedResult ? failedResult.FailureReason : "");

        public IDictionary<IParameter, object?>? Arguments { get; }

        public IDictionary<IParameter, MultiString>? RawArguments { get; }

        private readonly string _preview;

        public InvalidParameterArgumentParserResult(
            IDictionary<IParameter, object?> arguments,
            int position,
            string invalidToken,
            IResult result) 
        {
            Arguments = arguments;
            InvalidPosition = position;
            InvalidToken = invalidToken ?? throw new ArgumentNullException(nameof(invalidToken));
            Result = result;

            _preview = MessageHelpers.IsMention(invalidToken) ? invalidToken : Markdown.Escape(invalidToken.Truncate(15));
        }
    }
}
