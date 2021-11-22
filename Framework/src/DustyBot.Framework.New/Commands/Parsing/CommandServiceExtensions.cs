using System;
using System.Reflection;
using System.Threading.Tasks;
using Qmmands;

namespace DustyBot.Framework.Commands.Parsing
{
    public static class CommandServiceExtensions
    {
        private delegate Task<(TypeParseFailedResult TypeParseFailedResult, object ParsedArgument)> ParseArgumentAsyncDelegate(
            CommandService instance,
            Parameter parameter,
            object argument,
            CommandContext context);

        private static readonly ParseArgumentAsyncDelegate ParseArgumentAsyncDelegateInstance;

        static CommandServiceExtensions()
        {
            var method = typeof(CommandService).GetMethod("ParseArgumentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            ParseArgumentAsyncDelegateInstance = (ParseArgumentAsyncDelegate)Delegate.CreateDelegate(typeof(ParseArgumentAsyncDelegate), method);
        }

        /// <summary>
        /// Super ugly but necessary to access primitive type parsers from our custom <see cref="ArgumentParser"/>.
        /// </summary>
        public static Task<(TypeParseFailedResult TypeParseFailedResult, object ParsedArgument)> ParseArgumentAsync(
            this CommandService instance, 
            Parameter parameter, 
            object argument, 
            CommandContext context)
        {
            return ParseArgumentAsyncDelegateInstance(instance, parameter, argument, context);
        }
    }
}
