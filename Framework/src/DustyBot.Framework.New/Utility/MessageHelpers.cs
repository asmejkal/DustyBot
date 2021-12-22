using System.Text.RegularExpressions;

namespace DustyBot.Framework.Utility
{
    public class MessageHelpers
    {
        private static readonly Regex MentionRegex = new(@"^<(?:@!?|@&|#)[0-9]{17,21}>$", RegexOptions.Compiled);

        public static bool IsMention(string value) => MentionRegex.IsMatch(value);
    }
}
