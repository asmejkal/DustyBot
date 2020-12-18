namespace DustyBot.Framework.Commands.Parsing
{
    public class InvalidParameterCommandParseResult : CommandParseResult
    {
        public InvalidParameterCommandParseResult(CommandParseResultType type, int position) : base(type) => InvalidPosition = position;
        public int InvalidPosition { get; set; }
    }
}
