using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace DustyBot.Framework.Commands.Parsing
{
    public interface ICommandParser
    {
        string ParseInvoker(string message, string prefix);
        (CommandInfo, CommandInfo.Usage)? Match(string message, string prefix, IEnumerable<CommandInfo> commands);
        Task<CommandParseResult> Parse(IUserMessage message, CommandInfo registration, CommandInfo.Usage usage, string prefix);
        ICommand Create(SuccessCommandParseResult parseResult);
    }
}
