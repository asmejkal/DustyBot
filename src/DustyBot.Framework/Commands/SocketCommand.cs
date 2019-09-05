using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Communication;
using System.Text.RegularExpressions;

namespace DustyBot.Framework.Commands
{
    public class SocketCommand : ICommand
    {
        public enum ParseResultType
        {
            Success,
            InvalidPrefix,
            InvalidInvoker,
            MissingVerbs,
            InvalidVerb,
            NotEnoughParameters,
            InvalidParameterFormat,
            TooManyParameters
        }

        public class ParseResult
        {
            public ParseResult(ParseResultType type) => Type = type;
            public ParseResultType Type { get; set; }
        }

        public class InvalidParameterParseResult : ParseResult
        {
            public InvalidParameterParseResult(ParseResultType type, int position) : base(type) => InvalidPosition = position;
            public int InvalidPosition { get; set; }
        }

        public static readonly ICollection<char> TextQualifiers = new[] { '"', '“', '‟', '”', '＂' };

        private List<string> _verbs = new List<string>();
        private List<ParameterToken> _tokens = new List<ParameterToken>();

        public IUserMessage Message { get; private set; }
        public ulong GuildId => (Message.Channel as IGuildChannel)?.GuildId ?? 0;
        public IGuild Guild => (Message.Channel as IGuildChannel)?.Guild;
        public IUser Author => Message.Author;
        public IMessageChannel Channel => Message.Channel;

        public string Prefix => Config.CommandPrefix;
        public string Invoker { get; private set; }

        public IEnumerable<string> Verbs => _verbs;

        public string Body { get; private set; }

        public int ParametersCount => _tokens.Count;
        public int GetIndex(string name) => _tokens.FindIndex(x => string.Compare(x.Registration?.Name, name, true) == 0);
        public ParameterToken GetParameter(int key) => _tokens.ElementAtOrDefault(key) ?? ParameterToken.Empty;
        public ParameterToken GetParameter(string name) => _tokens.FirstOrDefault(x => string.Compare(x.Registration?.Name, name, true) == 0) ?? ParameterToken.Empty;
        public IEnumerable<ParameterToken> GetParameters() => _tokens;
        public ParameterToken this[int key] => GetParameter(key);
        public ParameterToken this[string name] => GetParameter(name);

        public Config.IEssentialConfig Config { get; set; }
        public CommandRegistration Registration { get; }
        public CommandRegistration.Usage Usage { get; }

        private SocketCommand(CommandRegistration registration, CommandRegistration.Usage usage, IUserMessage message, Config.IEssentialConfig config)
        {
            Registration = registration;
            Usage = usage;
            Message = message;
            Config = config;
        }

        public static async Task<Tuple<ParseResult, ICommand>> TryCreate(CommandRegistration registration, CommandRegistration.Usage usage, IUserMessage message, Config.IEssentialConfig config)
        {
            var command = new SocketCommand(registration, usage, message, config);
            return Tuple.Create(await command.TryParse(), command as ICommand);
        }

        public static string ParseInvoker(string message, string prefix) => new string(message.TakeWhile(c => !char.IsWhiteSpace(c)).Skip(prefix.Length).ToArray());

        public static (CommandRegistration, CommandRegistration.Usage)? FindLongestMatch(string message, string prefix, IEnumerable<CommandRegistration> possibleRegistrations)
        {
            var usageToRegistration = possibleRegistrations.SelectMany(x => x.EveryUsage.Select(y => (usage: y, registration: x))).ToList();
            var maxVerbs = possibleRegistrations.SelectMany(x => x.EveryUsage).Max(x => x.Verbs.Count);
            var split = message.Split(new char[0], maxVerbs + 2 /* (invoker and body) */, StringSplitOptions.RemoveEmptyEntries);
            var invoker = ParseInvoker(message, prefix);
            foreach (var (usage, registration) in usageToRegistration.OrderByDescending(x => x.usage.Verbs.Count))
            {
                if (string.Compare(invoker, usage.InvokeString) != 0)
                    continue;

                if (!usage.HasVerbs)
                    return (registration, usage);

                if (split.Length < usage.Verbs.Count + 1)
                    continue; //Not enough verbs in the message

                bool match = true;
                for (int i = 0; i < usage.Verbs.Count; ++i)
                {
                    if (string.Compare(split[i + 1], usage.Verbs[i], true) != 0)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return (registration, usage);
            }

            return null;
        }

        private async Task<ParseResult> TryParse()
        {
            //Prefix
            var content = Message.Content;
            if (!content.StartsWith(Prefix))
                return new ParseResult(ParseResultType.InvalidPrefix);

            //Invoker
            Invoker = ParseInvoker(content, Prefix);
            if (string.IsNullOrEmpty(Invoker) || string.Compare(Invoker, Usage.InvokeString, true) != 0)
                return new ParseResult(ParseResultType.InvalidInvoker);

            //Verbs
            var split = content.Split(new char[0], Usage.Verbs.Count + 2 /* (invoker and body) */, StringSplitOptions.RemoveEmptyEntries);
            if (Usage.HasVerbs)
            {
                if (split.Length < Usage.Verbs.Count + 1)
                    return new ParseResult(ParseResultType.MissingVerbs);

                for (int i = 0; i < Usage.Verbs.Count; ++i)
                {
                    if (string.Compare(split[i + 1], Usage.Verbs[i], true) != 0)
                        return new ParseResult(ParseResultType.InvalidVerb);

                    _verbs.Add(split[i + 1]);
                }
            }

            Body = split.ElementAtOrDefault(Usage.Verbs.Count + 1 /* (invoker) */) ?? string.Empty;

            return await TryParseParams();
        }

        private async Task<ParseResult> TryParseParams()
        {
            // Check for parser limitations
            if (Registration.Parameters.SkipLast().Any(x => x.Flags.HasFlag(ParameterFlags.Repeatable)))
                throw new InvalidOperationException($"Command '{Registration.PrimaryUsage.InvokeUsage}' cannot be parsed. Only the last parameter in a command can be repeatable.");

            return await TryParseParamsInner(Body.Tokenize(TextQualifiers.ToArray()).Select(x => new ParameterToken(x, Guild as SocketGuild)), Registration.Parameters);
        }
        
        private async Task<ParseResult> TryParseParamsInner(IEnumerable<ParameterToken> tokens, IEnumerable<ParameterRegistration> registrations, bool test = false)
        {
            var tokensQ = new Queue<ParameterToken>(tokens);
            int count = 0;
            foreach (var param in registrations)
            {
                count++;
                if (tokensQ.Count <= 0)
                {
                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                        continue;
                    else
                        return new ParseResult(ParseResultType.NotEnoughParameters);
                }

                // Extend the current token in case this parameter requires a remainder
                var token = tokensQ.Peek();
                if (param.Flags.HasFlag(ParameterFlags.Remainder))
                {
                    string value = Body.Substring(token.Begin);

                    // Handle the case when a user surrounds the remainder with quotes (even though they don't have to)
                    if (value.Length >= 2 && TextQualifiers.Contains(value.First()) && TextQualifiers.Contains(value.Last()))
                        token = new ParameterToken(new StringExtensions.Token() { Begin = token.Begin + 1, End = Body.Length - 1, Value = value.Substring(1, value.Length - 2) }, token.Guild);
                    else
                        token = new ParameterToken(new StringExtensions.Token() { Begin = token.Begin, End = Body.Length, Value = value }, token.Guild);
                }

                // Check if the token fits the parameter description
                if (!await CheckToken(token, param))
                {
                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                        continue;
                    else
                        return new InvalidParameterParseResult(ParseResultType.InvalidParameterFormat, ParametersCount + 1);
                }

                // If the parameter is optional, peek forward to check if we aren't stealing it from a required parameter
                var lastParam = registrations.Count() == count;
                if (param.Flags.HasFlag(ParameterFlags.Optional) && !lastParam)
                {
                    // Perform a testing run in the state we would be in if we accepted this token
                    var remainingTokens = param.Flags.HasFlag(ParameterFlags.Remainder) ? Enumerable.Empty<ParameterToken>() : tokensQ.Skip(1);
                    if ((await TryParseParamsInner(remainingTokens, registrations.Skip(count), true)).Type != ParseResultType.Success)
                    {
                        // The parsing would fail, so we can't take this token
                        token.Regex = null;
                        continue;
                    }
                }

                // If this is a non-testing run, add the token to result
                if (!test)
                {
                    token.Registration = param;
                    token.Regex = param.Type == ParameterType.Regex ? new Regex(param.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                    _tokens.Add(token);
                }

                // Remove the fitting token(s) from queue
                if (param.Flags.HasFlag(ParameterFlags.Remainder))
                    tokensQ.Clear();
                else
                    tokensQ.Dequeue();

                // If this is a repeatable (last) parameter, try to consume all remaining tokens
                if (lastParam && param.Flags.HasFlag(ParameterFlags.Repeatable))
                {
                    while (tokensQ.Any())
                    {
                        var remainingToken = tokensQ.Peek();
                        if (!await CheckToken(remainingToken, param))
                            break;

                        if (!test)
                        {
                            remainingToken.Regex = param.Type == ParameterType.Regex ? new Regex(param.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                            token.Repeats.Add(remainingToken);
                        }

                        tokensQ.Dequeue();
                    }
                }
            }

            if (tokensQ.Count > 0)
                return new ParseResult(ParseResultType.TooManyParameters);

            return new ParseResult(ParseResultType.Success);
        }

        private static async Task<bool> CheckToken(ParameterToken token, ParameterRegistration registration)
        {
            if (registration.Type == ParameterType.Regex)
                token.Regex = new Regex(registration.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            if (!(await token.IsType(registration.Type).ConfigureAwait(false)))
            {
                token.Regex = null;
                return false;
            }

            if (!string.IsNullOrEmpty(registration.Format) && Regex.IsMatch(token.Raw, registration.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) == registration.Inverse)
            {
                token.Regex = null;
                return false;
            }

            token.Regex = null;
            return true;
        }

        public Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message, Embed embed = null) => communicator.CommandReplySuccess(Message.Channel, message, embed);
        public Task<IUserMessage> ReplyError(ICommunicator communicator, string message) => communicator.CommandReplyError(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message) => communicator.CommandReply(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => communicator.CommandReply(Message.Channel, message, chunkDecorator, maxDecoratorOverhead);
        public Task Reply(ICommunicator communicator, PageCollection pages, bool controlledByInvoker = false, bool resend = false) => communicator.CommandReply(Message.Channel, pages, controlledByInvoker ? Message.Author.Id : 0, resend);
    }
}
