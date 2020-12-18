using Discord;
using Discord.WebSocket;
using DustyBot.Core.Collections;
using DustyBot.Core.Parsing;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands.Parsing
{
    internal class CommandParser : ICommandParser
    {
        public static readonly ICollection<char> TextQualifiers = new[] { '"', '“', '‟', '”', '＂' };

        private readonly IUserFetcher _userFetcher;
        private readonly ICommunicator _communicator;

        public CommandParser(IUserFetcher userFetcher, ICommunicator communicator)
        {
            _userFetcher = userFetcher ?? throw new ArgumentNullException(nameof(userFetcher));
            _communicator = communicator;
        }

        public string ParseInvoker(string message, string prefix) => 
            new string(message.TakeWhile(c => !char.IsWhiteSpace(c)).Skip(prefix.Length).ToArray());

        public (CommandInfo, CommandInfo.Usage)? Match(string message, string prefix, IEnumerable<CommandInfo> commands)
        {
            var usageToRegistration = commands.SelectMany(x => x.EveryUsage.Select(y => (usage: y, registration: x))).ToList();
            var maxVerbs = commands.SelectMany(x => x.EveryUsage).Max(x => x.Verbs.Count);
            var split = message.Split(new char[0], maxVerbs + 2 /* (invoker and body) */, StringSplitOptions.RemoveEmptyEntries);
            var invoker = ParseInvoker(message, prefix);
            foreach (var (usage, registration) in usageToRegistration.OrderByDescending(x => x.usage.Verbs.Count))
            {
                if (string.Compare(invoker, usage.InvokeString, true, CultureInfo.InvariantCulture) != 0)
                    continue;

                if (!usage.HasVerbs)
                    return (registration, usage);

                if (split.Length < usage.Verbs.Count + 1)
                    continue; //Not enough verbs in the message

                bool match = true;
                for (int i = 0; i < usage.Verbs.Count; ++i)
                {
                    if (string.Compare(split[i + 1], usage.Verbs[i], true, CultureInfo.InvariantCulture) != 0)
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

        public async Task<CommandParseResult> Parse(IUserMessage message, CommandInfo registration, CommandInfo.Usage usage, string prefix)
        {
            //Prefix
            var content = message.Content;
            if (!content.StartsWith(prefix))
                return new CommandParseResult(CommandParseResultType.InvalidPrefix);

            //Invoker
            var invoker = ParseInvoker(content, prefix);
            if (string.IsNullOrEmpty(invoker) || string.Compare(invoker, usage.InvokeString, true) != 0)
                return new CommandParseResult(CommandParseResultType.InvalidInvoker);

            //Verbs
            var verbs = new List<string>();
            var split = content.Split(new char[0], usage.Verbs.Count + 2 /* (invoker and body) */, StringSplitOptions.RemoveEmptyEntries);
            if (usage.HasVerbs)
            {
                if (split.Length < usage.Verbs.Count + 1)
                    return new CommandParseResult(CommandParseResultType.MissingVerbs);

                for (int i = 0; i < usage.Verbs.Count; ++i)
                {
                    if (string.Compare(split[i + 1], usage.Verbs[i], true) != 0)
                        return new CommandParseResult(CommandParseResultType.InvalidVerb);

                    verbs.Add(split[i + 1]);
                }
            }

            var body = split.ElementAtOrDefault(usage.Verbs.Count + 1 /* invoker */) ?? string.Empty;

            // Check for parser limitations
            if (registration.Parameters.SkipLast().Any(x => x.Flags.HasFlag(ParameterFlags.Repeatable)))
                throw new InvalidOperationException($"Command \"{registration.PrimaryUsage.InvokeUsage}\" cannot be parsed. Only the last parameter in a command can be repeatable.");

            var guild = (SocketGuild)(message.Channel as IGuildChannel)?.Guild;
            var possibleTokens = body.Tokenize(TextQualifiers.ToArray()).Select(x => new ParameterToken(x, guild, _userFetcher));
            var usedTokens = new List<ParameterToken>();
            var paramsResult = await ParseParameters(body, possibleTokens, registration.Parameters, usedTokens);
            if (paramsResult.Type != CommandParseResultType.Success)
                return paramsResult;

            return new SuccessCommandParseResult(invoker, verbs, body, usedTokens, registration, usage, prefix, message);
        }

        public ICommand Create(SuccessCommandParseResult parseResult)
        {
            return new Command(parseResult, _communicator);
        }

        private async Task<CommandParseResult> ParseParameters(string body, IEnumerable<ParameterToken> possibleTokens, IEnumerable<ParameterInfo> parameters, ICollection<ParameterToken> usedTokens, bool test = false)
        {
            var tokensQueue = new Queue<ParameterToken>(possibleTokens);
            int count = 0;
            foreach (var param in parameters)
            {
                count++;
                if (tokensQueue.Count <= 0)
                {
                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                        continue;
                    else
                        return new CommandParseResult(CommandParseResultType.NotEnoughParameters);
                }

                var token = tokensQueue.Peek();

                // Extend the current token in case this parameter requires a remainder
                bool? remainderMatch = null;
                if (param.Flags.HasFlag(ParameterFlags.Remainder))
                {
                    string value = body.Substring(token.Begin);
                    ParameterToken remainder;

                    // Handle the case when a user surrounds the remainder with quotes (even though they don't have to)
                    if (value.Length >= 2 && TextQualifiers.Contains(value.First()) && TextQualifiers.Contains(value.Last()))
                        remainder = new ParameterToken(new Token() { Begin = token.Begin + 1, End = body.Length - 1, Value = value.Substring(1, value.Length - 2) }, token.Guild, _userFetcher);
                    else
                        remainder = new ParameterToken(new Token() { Begin = token.Begin, End = body.Length, Value = value }, token.Guild, _userFetcher);

                    remainderMatch = await CheckToken(remainder, param);
                    if (remainderMatch.Value)
                    {
                        token = remainder;
                    }
                    else if (param.Flags.HasFlag(ParameterFlags.Repeatable))
                    {
                        remainderMatch = null; // Give it a second chance as a repeatable parameter
                    }
                }

                // Check if the token fits the parameter description
                if (!(remainderMatch ?? await CheckToken(token, param)))
                {
                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                        continue;
                    else
                        return new InvalidParameterCommandParseResult(CommandParseResultType.InvalidParameterFormat, usedTokens.Count + 1);
                }

                // If the parameter is optional, peek forward to check if we aren't stealing it from a required parameter
                var lastParam = parameters.Count() == count;
                if (param.Flags.HasFlag(ParameterFlags.Optional) && !lastParam)
                {
                    // Perform a testing run in the state we would be in if we accepted this token
                    var remainingTokens = param.Flags.HasFlag(ParameterFlags.Remainder) ? Enumerable.Empty<ParameterToken>() : tokensQueue.Skip(1);
                    if ((await ParseParameters(body, remainingTokens, parameters.Skip(count), usedTokens, test: true)).Type != CommandParseResultType.Success)
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
                    usedTokens.Add(token);
                }

                // Remove the fitting token(s) from queue
                if (remainderMatch ?? false)
                    tokensQueue.Clear();
                else
                    tokensQueue.Dequeue();

                // If this is a repeatable (last) parameter, try to consume all remaining tokens
                if (lastParam && param.Flags.HasFlag(ParameterFlags.Repeatable))
                {
                    while (tokensQueue.Any())
                    {
                        var remainingToken = tokensQueue.Peek();
                        if (!await CheckToken(remainingToken, param))
                            break;

                        if (!test)
                        {
                            remainingToken.Regex = param.Type == ParameterType.Regex ? new Regex(param.Format, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                            token.Repeats.Add(remainingToken);
                        }

                        tokensQueue.Dequeue();
                    }
                }
            }

            if (tokensQueue.Count > 0)
                return new CommandParseResult(CommandParseResultType.TooManyParameters);

            return new CommandParseResult(CommandParseResultType.Success);
        }

        private static async Task<bool> CheckToken(ParameterToken token, ParameterInfo registration)
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
    }
}
