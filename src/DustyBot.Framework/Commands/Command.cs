using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Commands.Parsing;

namespace DustyBot.Framework.Commands
{
    internal sealed class Command : ICommand
    {
        public ParameterToken this[int key] => GetParameter(key);
        public ParameterToken this[string name] => GetParameter(name);

        public int ParametersCount => _tokens.Count;
        public IUserMessage Message { get; }
        public ulong GuildId => (Message.Channel as IGuildChannel)?.GuildId ?? 0;
        public IGuild Guild => (Message.Channel as IGuildChannel)?.Guild;
        public IUser Author => Message.Author;
        public IMessageChannel Channel => Message.Channel;

        public string Prefix { get; }
        public string Invoker { get; }

        public IEnumerable<string> Verbs => _verbs;

        public string Body { get; }

        public CommandInfo Registration { get; }
        public CommandInfo.Usage Usage { get; }

        private readonly IReadOnlyCollection<string> _verbs = new List<string>();
        private readonly IReadOnlyCollection<ParameterToken> _tokens = new List<ParameterToken>();
        private readonly ICommunicator _communicator;

        public Command(SuccessCommandParseResult parseResult, ICommunicator communicator)
        {
            Registration = parseResult.Registration;
            Usage = parseResult.Usage;
            Message = parseResult.Message;
            Prefix = parseResult.Prefix;
            Body = parseResult.Body;
            Invoker = parseResult.Invoker;
            _verbs = parseResult.Verbs;
            _tokens = parseResult.Tokens;

            _communicator = communicator;
        }

        public ParameterToken GetParameter(int key) => _tokens.ElementAtOrDefault(key) ?? ParameterToken.Empty;
        public ParameterToken GetParameter(string name) => _tokens.FirstOrDefault(x => string.Compare(x.Registration?.Name, name, true) == 0) ?? ParameterToken.Empty;
        public IEnumerable<ParameterToken> GetParameters() => _tokens;
        public string GetUsage() => _communicator.BuildCommandUsage(Registration, Prefix);

        public Task<ICollection<IUserMessage>> ReplySuccess(string message, Embed embed = null) => _communicator.CommandReplySuccess(Message.Channel, message, embed);
        public Task<ICollection<IUserMessage>> ReplyError(string message) => _communicator.CommandReplyError(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(string message) => _communicator.CommandReply(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(Embed embed) => _communicator.CommandReply(Message.Channel, embed);
        public Task<ICollection<IUserMessage>> Reply(string message, Embed embed) => _communicator.CommandReply(Message.Channel, message, embed);
        public Task<ICollection<IUserMessage>> Reply(string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => _communicator.CommandReply(Message.Channel, message, chunkDecorator, maxDecoratorOverhead);
        public Task Reply(PageCollection pages, bool controlledByInvoker = false, bool resend = false) => _communicator.CommandReply(Message.Channel, pages, controlledByInvoker ? Message.Author.Id : 0, resend);
    }
}
