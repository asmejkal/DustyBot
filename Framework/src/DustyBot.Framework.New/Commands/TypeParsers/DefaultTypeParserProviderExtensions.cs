using System.Collections.Generic;
using Qmmands;
using Qmmands.Default;

namespace DustyBot.Framework.Commands.TypeParsers
{
    internal static class DefaultTypeParserProviderExtensions
    {
        public static void ReplaceParser(this DefaultTypeParserProvider x, ITypeParser parser)
        {
            var parsers = x.TypeParsers.GetOrAdd(parser.ParsedType, _ => new List<ITypeParser>());
            lock (parsers)
            {
                parsers.Clear();
                parsers.Add(parser);
            }
        }
    }
}
