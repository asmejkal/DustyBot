namespace DustyBot.Framework.Commands.Parsing
{
    public class CommandParseResult
    {
        public CommandParseResult(CommandParseResultType type) => Type = type;
        public CommandParseResultType Type { get; set; }
    }
}
