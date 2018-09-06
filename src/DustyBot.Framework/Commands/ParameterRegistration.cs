using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.Commands
{
    public class ParameterRegistration
    {
        public const int InfiniteRepeats = -1;

        public string Name { get; set; }
        public string Format { get; set; }
        public ParameterType Type { get; set; }
        public ParameterFlags Flags { get; set; }
        public string Description { private get; set; }

        public int MinRepeats { get; set; } = 1;
        int _maxRepeats = 1;
        public int MaxRepeats { get => Flags.HasFlag(ParameterFlags.Remainder) ? 1 : _maxRepeats; set => _maxRepeats = value; }

        public string GetDescription(string prefix) => Description?.Replace(CommandRegistration.PrefixWildcard, prefix);
    }
}
