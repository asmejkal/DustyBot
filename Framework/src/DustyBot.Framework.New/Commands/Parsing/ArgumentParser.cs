using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Core.Parsing;
using DustyBot.Framework.Commands.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;
using Qmmands.Text;
using Qmmands.Text.Default;
using Qommon;

namespace DustyBot.Framework.Commands.Parsing
{
    public class ArgumentParser : IArgumentParser
    {
        private class CommandParsingContext
        {
            public ITextCommandContext CommandContext { get; }
            public ICommandService CommandService { get; }
            public Dictionary<IParameter, object?> Results { get; } = new();
            public int TotalTokenCount { get; }

            public Dictionary<(IParameter, Token), (IResult Result, object? Value)> ParseResultCache { get; } = new();

            // Enumerable ("params"/array) parameters go here instead of Results, as raw per-token strings
            // rather than already-parsed values: Qmmands' own TypeParse execution step reads RawArguments
            // to build the parameter's actual declared collection type (T[] vs List<T>), which a plain
            // List<object?> in Arguments can't satisfy (Qmmands validates the bound argument's runtime type
            // against the parameter, with no implicit list-to-array conversion). TypeParse only fills in
            // parameters absent from Arguments, so single-value parameters (already fully resolved below)
            // are left alone.
            public Dictionary<IParameter, List<ReadOnlyMemory<char>>> RawArguments { get; } = new();

            public CommandParsingContext(ITextCommandContext commandContext, ICommandService commandService, int totalTokenCount)
            {
                CommandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
                CommandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
                TotalTokenCount = totalTokenCount;
            }

            public void PromoteResultFromCache(IParameter param, Token token)
            {
                if (!ParseResultCache.TryGetValue((param, token), out var value))
                    throw new InvalidOperationException("Parse result missing in cache.");

                if (param.GetTypeInformation().IsEnumerable)
                {
                    if (!RawArguments.TryGetValue(param, out var rawValues))
                        RawArguments.Add(param, rawValues = new List<ReadOnlyMemory<char>>());

                    rawValues.Add(token.Value.AsMemory());
                }
                else
                {
                    Results.Add(param, value.Value);
                }
            }
        }

        private readonly IReadOnlyDictionary<char, char> _quotationMarks = DefaultArgumentParserConfiguration.DefaultQuotationMarks;

        public bool SupportsOptionalParameters => true;

        public ArgumentParser()
        {
        }

        public ValueTask<IArgumentParserResult> ParseAsync(ITextCommandContext context)
        {
            var commandService = context.Services.GetRequiredService<ICommandService>();

            var arguments = context.RawArgumentString.Span.ToString();
            var tokens = arguments.Tokenize(_quotationMarks).ToList();
            var parsingContext = new CommandParsingContext(context, commandService, tokens.Count);

            return ParseParameters(arguments, tokens, context.Command.Parameters, parsingContext);
        }

        private async ValueTask<IArgumentParserResult> ParseParameters(
            string body, 
            IEnumerable<Token> tokens, 
            IEnumerable<ITextParameter> parameters, 
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
                    if (param.HasDefaultValue())
                    {
                        if (!peek)
                            context.Results.Add(param, param.DefaultValue.GetValueOrDefault());

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
                var isRemainderParam = param is IPositionalParameter positionalParam && positionalParam.IsRemainder;
                if (isRemainderParam)
                {
                    string value = body.Substring(token.Begin);
                    Token remainder;

                    // Handle the case when a user surrounds the remainder with quotes (even though they don't have to)
                    if (value.Length >= 2 
                        && _quotationMarks.ContainsKey(value.First()) 
                        && _quotationMarks[value.First()] == value.Last())
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
                    else if (param.GetTypeInformation().IsEnumerable)
                    {
                        remainderMatch = null; // Give it a second chance as a repeatable parameter
                    }
                }

                // Check if the token fits the parameter description
                if (!(remainderMatch ?? await CheckToken(token, param, context)))
                {
                    if (param.HasDefaultValue())
                    {
                        if (!peek)
                            context.Results.Add(param, param.DefaultValue.GetValueOrDefault());

                        continue;
                    }
                    else
                    {
                        return new InvalidParameterArgumentParserResult(
                            context.Results, 
                            context.TotalTokenCount - tokensQueue.Count + 1, 
                            token.Value, 
                            context.ParseResultCache[(param, token)].Result);
                    }
                }

                // If the parameter is optional, peek forward to check if we aren't stealing it from a required parameter
                var lastParam = parameters.Count() == count;
                if (param.HasDefaultValue() && !lastParam)
                {
                    // Perform a testing run in the state we would be in if we accepted this token
                    var remainingTokens = isRemainderParam ? Enumerable.Empty<Token>() : tokensQueue.Skip(1);
                    var peekResult = await ParseParameters(body, remainingTokens, parameters.Skip(count), context, peek: true);

                    if (!peekResult.IsSuccessful)
                    {
                        if (!peek)
                            context.Results.Add(param, param.DefaultValue.GetValueOrDefault());

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
                if (lastParam && param.GetTypeInformation().IsEnumerable)
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

            return new SuccessArgumentParserResult(context.Results, BuildRawArguments(context.RawArguments));
        }

        private static IDictionary<IParameter, MultiString>? BuildRawArguments(Dictionary<IParameter, List<ReadOnlyMemory<char>>> rawArguments)
        {
            if (rawArguments.Count == 0)
                return null;

            return rawArguments.ToDictionary(x => x.Key, x => new MultiString(x.Value));
        }

        private static async Task<bool> CheckToken(Token token, ITextParameter parameter, CommandParsingContext context)
        {
            if (context.ParseResultCache.TryGetValue((parameter, token), out var result))
                return result.Result.IsSuccessful;

            var value = token.Value.AsMemory();
            var typeParserProvider = context.CommandContext.Services.GetRequiredService<ITypeParserProvider>();
            var typeParser = typeParserProvider.GetParser(parameter);

            object? parsedArgument;
            if (typeParser != null)
            {
                var typeParserResult = await typeParser.ParseAsync(context.CommandContext, parameter, value).ConfigureAwait(false);
                if (!typeParserResult.IsSuccessful)
                {
                    context.ParseResultCache[(parameter, token)] = (new TypeParseFailedResult(parameter, value, typeParserResult.FailureReason), null);
                    return false;
                }

                parsedArgument = typeParserResult.ParsedValue.GetValueOrDefault();
            }
            else if (parameter.GetTypeInformation().IsStringLike)
            {
                parsedArgument = token.Value;
            }
            else
            {
                throw new InvalidOperationException($"No type parser found for parameter {parameter.Name}.");
            }

            var checksResult = await RunParameterChecksAsync(parameter, parsedArgument, context.CommandContext).ConfigureAwait(false);
            context.ParseResultCache[(parameter, token)] = (checksResult, parsedArgument);
            return checksResult.IsSuccessful;
        }

        private static async Task<IResult> RunParameterChecksAsync(ITextParameter parameter, object? argument, ITextCommandContext context)
        {
            foreach (var check in parameter.Checks)
            {
                if (!check.CanCheck(parameter, argument))
                    continue;

                var result = await check.CheckAsync(context, parameter, argument).ConfigureAwait(false);
                if (!result.IsSuccessful)
                    return result;
            }

            return Qmmands.Results.Success;
        }

        public void Validate(ITextCommand command)
        {
            foreach (var parameter in command.Parameters)
            {
                if (parameter is not IPositionalParameter)
                    throw new ArgumentException($"The command {command.Name} can not be parsed by {nameof(ArgumentParser)} because it contains non-positional parameters.", nameof(command));
            }
        }
    }
}
