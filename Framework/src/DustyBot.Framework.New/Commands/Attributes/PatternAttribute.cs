using System;
using System.Text.RegularExpressions;

namespace DustyBot.Framework.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class PatternAttribute : Attribute
    {
        public Regex Regex { get; }

        public PatternAttribute(string regexPattern, RegexOptions options = RegexOptions.None)
        {
            Regex = new Regex(regexPattern, options | RegexOptions.Compiled);
        }
    }
}
