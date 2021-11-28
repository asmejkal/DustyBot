using System.Collections.Generic;
using Disqord;
using DustyBot.Core.Formatting;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public class InvalidParameterArgumentParserResult : ArgumentParserResult
    {
        public int InvalidPosition { get; set; }
        public string InvalidToken { get; }

        public override bool IsSuccessful => false;

        public override string FailureReason => $"Parameter {InvalidPosition} ({Markdown.Escape(InvalidToken.Truncate(15))}) is invalid.";

        public InvalidParameterArgumentParserResult(IReadOnlyDictionary<Parameter, object?> arguments, int position, string invalidToken) 
            : base(arguments)
        {
            InvalidPosition = position;
            InvalidToken = invalidToken;
        }
    }
}
