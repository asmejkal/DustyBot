using System.Collections.Generic;
using Discord;

namespace DustyBot.Framework.Commands.Parsing
{
    public class SuccessCommandParseResult : CommandParseResult
    {
        public string Invoker { get; }
        public IReadOnlyCollection<string> Verbs { get; }
        public string Body { get; }
        public IReadOnlyCollection<ParameterToken> Tokens { get; }
        public CommandInfo Registration { get; }
        public CommandInfo.Usage Usage { get; }
        public string Prefix { get; }
        public IUserMessage Message { get; }

        public SuccessCommandParseResult(
            string invoker, 
            IReadOnlyCollection<string> verbs, 
            string body, 
            IReadOnlyCollection<ParameterToken> tokens,
            CommandInfo registration, 
            CommandInfo.Usage usage, 
            string prefix,
            IUserMessage message)
            : base(CommandParseResultType.Success)
        {
            Invoker = invoker;
            Verbs = verbs;
            Body = body;
            Tokens = tokens;
            Registration = registration;
            Usage = usage;
            Prefix = prefix;
            Message = message;
        }
    }
}
