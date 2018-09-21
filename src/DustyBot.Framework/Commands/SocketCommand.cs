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

        private List<string> _verbs = new List<string>();
        private List<ParameterToken> _tokens = new List<ParameterToken>();

        public IUserMessage Message { get; private set; }
        public ulong GuildId => (Message.Channel as IGuildChannel)?.GuildId ?? 0;
        public IGuild Guild => (Message.Channel as IGuildChannel)?.Guild;

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

        private SocketCommand(CommandRegistration registration, SocketUserMessage message, Config.IEssentialConfig config)
        {
            Registration = registration;
            Message = message;
            Config = config;
        }

        public static async Task<Tuple<ParseResult, ICommand>> TryCreate(CommandRegistration registration, SocketUserMessage message, Config.IEssentialConfig config)
        {
            var command = new SocketCommand(registration, message, config);
            return Tuple.Create(await command.TryParse(), command as ICommand);
        }

        public static string ParseInvoker(string message, string prefix) => new string(message.TakeWhile(c => !char.IsWhiteSpace(c)).Skip(prefix.Length).ToArray());

        public static CommandRegistration FindLongestMatch(string message, IEnumerable<CommandRegistration> possibleRegistrations)
        {
            var maxVerbs = possibleRegistrations.Max(x => x.Verbs.Count);
            var split = message.Split(new char[0], maxVerbs + 2 /* (invoker and body) */, StringSplitOptions.RemoveEmptyEntries);
            foreach (var r in possibleRegistrations.OrderByDescending(x => x.Verbs.Count))
            {
                if (!r.HasVerbs)
                    return r;

                if (split.Length < r.Verbs.Count + 1)
                    continue; //Not enough verbs in the message

                bool match = true;
                for (int i = 0; i < r.Verbs.Count; ++i)
                {
                    if (string.Compare(split[i + 1], r.Verbs[i], true) != 0)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return r;
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
            if (string.IsNullOrEmpty(Invoker) || string.Compare(Invoker, Registration.InvokeString, true) != 0)
                return new ParseResult(ParseResultType.InvalidInvoker);

            //Verbs
            var split = content.Split(new char[0], Registration.Verbs.Count + 2 /* (invoker and body) */, StringSplitOptions.RemoveEmptyEntries);
            if (Registration.HasVerbs)
            {
                if (split.Length < Registration.Verbs.Count + 1)
                    return new ParseResult(ParseResultType.MissingVerbs);

                for (int i = 0; i < Registration.Verbs.Count; ++i)
                {
                    if (string.Compare(split[i + 1], Registration.Verbs[i], true) != 0)
                        return new ParseResult(ParseResultType.InvalidVerb);

                    _verbs.Add(split[i + 1]);
                }
            }

            Body = split.ElementAtOrDefault(Registration.Verbs.Count + 1 /* (invoker) */) ?? string.Empty;

            return await TryParseParams();
        }

        private async Task<ParseResult> TryParseParams() 
            => await TryParseParamsInner(Body.Tokenize('"').Select(x => new ParameterToken(x, Guild as SocketGuild)), Registration.Parameters);

        //TODO: overcomplicated mess
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

                if (!param.Flags.HasFlag(ParameterFlags.Repeatable))
                {
                    var token = tokensQ.Peek();
                    if (param.Flags.HasFlag(ParameterFlags.Remainder))
                    {
                        string value = Body.Substring(token.Begin);
                        if (value.StartsWith("\"") && value.EndsWith("\""))
                            token = new ParameterToken(new StringExtensions.Token() { Begin = token.Begin + 1, End = Body.Length - 1, Value = value.Substring(1, value.Length - 2) }, token.Guild);
                        else
                            token = new ParameterToken(new StringExtensions.Token() { Begin = token.Begin, End = Body.Length, Value = value }, token.Guild);
                    }

                    if (!await CheckToken(token, param))
                    {
                        if (param.Flags.HasFlag(ParameterFlags.Optional))
                            continue;
                        else
                            return new InvalidParameterParseResult(ParseResultType.InvalidParameterFormat, ParametersCount + 1);
                    }

                    if (param.Flags.HasFlag(ParameterFlags.Remainder))
                    {
                        if (!test)
                        {
                            token.Registration = param;
                            _tokens.Add(token);
                        }

                        return new ParseResult(ParseResultType.Success);
                    }

                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                    {
                        //Peek forward to check if we aren't stealing this token from a required parameter
                        if ((await TryParseParamsInner(tokensQ.Skip(1), registrations.Skip(count), true)).Type != ParseResultType.Success)
                        {
                            token.Regex = null;
                            continue; //The parsing would fail, so we can't take the token
                        }
                    }

                    if (!test)
                    {
                        token.Registration = param;
                        token.Regex = param.Type == ParameterType.Regex ? new Regex(param.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                        _tokens.Add(token);
                    }

                    tokensQ.Dequeue();
                }
                else
                {
                    var token = tokensQ.Peek();
                    if (!await CheckToken(token, param))
                    {
                        if (param.Flags.HasFlag(ParameterFlags.Optional))
                            continue;
                        else
                            return new InvalidParameterParseResult(ParseResultType.InvalidParameterFormat, ParametersCount + 1);
                    }

                    var optParam = new ParameterRegistration(param);
                    optParam.Flags = param.Flags | ParameterFlags.Optional;
                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                    {
                        //Peek forward to check if we aren't stealing this token from a required parameter
                        if ((await TryParseParamsInner(tokensQ.Skip(1), registrations.Skip(count).Prepend(optParam), true)).Type != ParseResultType.Success)
                            continue; //The parsing would fail, so we can't take the token
                    }

                    if (!test)
                    {
                        token.Registration = param;
                        token.Regex = param.Type == ParameterType.Regex ? new Regex(param.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                        _tokens.Add(token);
                    }

                    tokensQ.Dequeue();

                    do
                    {
                        if (tokensQ.Count <= 0)
                            break;

                        var repeat = tokensQ.Peek();
                        if (!await CheckToken(repeat, optParam))
                            break;

                        //Peek forward to check if we aren't stealing this token from a required parameter
                        if ((await TryParseParamsInner(tokensQ.Skip(1), registrations.Skip(count).Prepend(optParam), true)).Type != ParseResultType.Success)
                            break;

                        if (!test)
                        {
                            repeat.Regex = optParam.Type == ParameterType.Regex ? new Regex(optParam.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                            token.Repeats.Add(repeat);
                        }

                        tokensQ.Dequeue();
                    } while (true);
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

        public Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message) => communicator.CommandReplySuccess(Message.Channel, message);
        public Task<IUserMessage> ReplyError(ICommunicator communicator, string message) => communicator.CommandReplyError(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message) => communicator.CommandReply(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => communicator.CommandReply(Message.Channel, message, chunkDecorator, maxDecoratorOverhead);
        public Task Reply(ICommunicator communicator, PageCollection pages, bool controlledByInvoker = false, bool resend = false) => communicator.CommandReply(Message.Channel, pages, controlledByInvoker ? Message.Author.Id : 0, resend);
    }
}
