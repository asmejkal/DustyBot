using System;
using System.Linq;
using Qmmands.Text;
using Qmmands.Text.Default;

namespace DustyBot.Framework.Commands
{
    public static class CommandMapExtensions
    {
        /// <summary>
        /// <see cref="ITextCommandMap.FindMatches"/> returns candidates in tree-traversal order (a group's own
        /// command comes before its subcommands' matches), not sorted by specificity - so for a group like
        /// `views`/`views add`, the first entry for "views add" is "views" itself, not "views add". This applies
        /// the same ordering Qmmands' own MapLookup execution step uses to pick which overload to actually run,
        /// so callers that want "the command this input refers to" get the same, most-specific match.
        /// </summary>
        public static ITextCommandMatch? FindBestMatch(this ITextCommandMap map, ReadOnlyMemory<char> input) =>
            map.FindMatches(input).OrderBy(x => x, CommandOverloadComparer.Instance).FirstOrDefault();
    }
}
