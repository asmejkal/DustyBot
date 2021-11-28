using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Core.Parsing;
using DustyBot.Framework.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public class ArgumentParser : IArgumentParser
    {
        private class CommandParsingContext
        {
            public CommandContext CommandContext { get; }
            public CommandService CommandService { get; }
            public Dictionary<Parameter, object?> Results { get; } = new();
            public int TotalTokenCount { get; }

            public Dictionary<(Parameter, Token), (bool IsSuccessful, object? Result)> ParseResultCache { get; } = new();

            public CommandParsingContext(CommandContext commandContext, CommandService commandService, int totalTokenCount)
            {
                CommandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
                CommandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
                TotalTokenCount = totalTokenCount;
            }

            public void PromoteResultFromCache(Parameter param, Token token)
            {
                if (!ParseResultCache.TryGetValue((param, token), out var value))
                    throw new InvalidOperationException("Parse result missing in cache.");

                if (param.IsMultiple)
                {
                    if (!Results.TryGetValue(param, out var values) || values is not List<object?> valuesList)
                        Results.Add(param, valuesList = new List<object?>());

                    valuesList.Add(value.Result);
                }
                else
                {
                    Results.Add(param, value.Result);
                }
            }
        }

        public ArgumentParser()
        {
        }

        public ValueTask<ArgumentParserResult> ParseAsync(CommandContext context)
        {
            var commandService = context.Services.GetRequiredService<CommandService>();

            var tokens = context.RawArguments.Tokenize(commandService.QuotationMarkMap).ToList();
            var parsingContext = new CommandParsingContext(context, commandService, tokens.Count);

            return ParseParameters(context.RawArguments, tokens, context.Command.Parameters, parsingContext);
        }

        private async ValueTask<ArgumentParserResult> ParseParameters(
            string body, 
            IEnumerable<Token> tokens, 
            IEnumerable<Parameter> parameters, 
            CommandParsingContext context,
            bool peek = false)
        {
            var tokensQueue = new Queue<Token>(tokens);
            int count = 0;
            foreach (var param in parameters)
            {
                count++;
                if (tokensQueue.Count <= 0)
                {
                    if (param.IsOptional || param.Attributes.Any(x => x is DefaultAttribute))
                    {
                        if (!peek)
                            context.Results.Add(param, param.DefaultValue);

                        continue;
                    }
                    else
                    {
                        return new FailureArgumentParserResult(context.Results, ArgumentParserFailureType.NotEnoughParameters);
                    }
                }

                var token = tokensQueue.Peek();

                // Extend the current token in case this parameter requires a remainder
                bool? remainderMatch = null;
                if (param.IsRemainder)
                {
                    string value = body.Substring(token.Begin);
                    Token remainder;

                    // Handle the case when a user surrounds the remainder with quotes (even though they don't have to)
                    if (value.Length >= 2 
                        && context.CommandService.QuotationMarkMap.ContainsKey(value.First()) 
                        && context.CommandService.QuotationMarkMap[value.First()] == value.Last())
                    {
                        remainder = new Token() { Begin = token.Begin + 1, End = body.Length - 1, Value = value.Substring(1, value.Length - 2) };
                    }
                    else
                    {
                        remainder = new Token() { Begin = token.Begin, End = body.Length, Value = value };
                    }

                    remainderMatch = await CheckToken(remainder, param, context);
                    if (remainderMatch.Value)
                    {
                        token = remainder;
                    }
                    else if (param.IsMultiple)
                    {
                        remainderMatch = null; // Give it a second chance as a repeatable parameter
                    }
                }

                // Check if the token fits the parameter description
                if (!(remainderMatch ?? await CheckToken(token, param, context)))
                {
                    if (param.IsOptional || param.Attributes.Any(x => x is DefaultAttribute))
                    {
                        if (!peek)
                            context.Results.Add(param, param.DefaultValue);

                        continue;
                    }
                    else
                    {
                        return new InvalidParameterArgumentParserResult(context.Results, context.TotalTokenCount - tokensQueue.Count + 1, token.Value);
                    }
                }

                // If the parameter is optional, peek forward to check if we aren't stealing it from a required parameter
                var lastParam = parameters.Count() == count;
                if ((param.IsOptional || param.Attributes.Any(x => x is DefaultAttribute)) && !lastParam)
                {
                    // Perform a testing run in the state we would be in if we accepted this token
                    var remainingTokens = param.IsRemainder ? Enumerable.Empty<Token>() : tokensQueue.Skip(1);
                    var peekResult = await ParseParameters(body, remainingTokens, parameters.Skip(count), context, peek: true);

                    if (!peekResult.IsSuccessful)
                    {
                        if (!peek)
                            context.Results.Add(param, param.DefaultValue);

                        continue; // The parsing would fail, so we can't take this token
                    }
                }

                // If this is a non-testing run, add the token to result
                if (!peek)
                    context.PromoteResultFromCache(param, token);

                // Remove the fitting token(s) from queue
                if (remainderMatch ?? false)
                    tokensQueue.Clear();
                else
                    tokensQueue.Dequeue();

                // If this is a repeatable (last) parameter, try to consume all remaining tokens
                if (lastParam && param.IsMultiple)
                {
                    while (tokensQueue.Any())
                    {
                        var remainingToken = tokensQueue.Peek();
                        if (!await CheckToken(remainingToken, param, context))
                            break;

                        if (!peek)
                            context.PromoteResultFromCache(param, remainingToken);

                        tokensQueue.Dequeue();
                    }
                }
            }

            if (tokensQueue.Count > 0)
                return new FailureArgumentParserResult(context.Results, ArgumentParserFailureType.TooManyParameters);

            return new SuccessArgumentParserResult(context.Results);
        }

        private static async Task<bool> CheckToken(Token token, Parameter parameter, CommandParsingContext context)
        {
            if (context.ParseResultCache.TryGetValue((parameter, token), out var result))
                return result.IsSuccessful;

            var (failedResult, parsedArgument) = await context.CommandService.ParseArgumentAsync(parameter, token.Value, context.CommandContext);
            if (failedResult == null)
            {
                var checksResult = await parameter.RunChecksAsync(parsedArgument, context.CommandContext);
                if (checksResult.IsSuccessful)
                {
                    context.ParseResultCache[(parameter, token)] = (true, parsedArgument);
                    return true;
                }
            }

            context.ParseResultCache[(parameter, token)] = (false, null);
            return false;
        }
    }
}
