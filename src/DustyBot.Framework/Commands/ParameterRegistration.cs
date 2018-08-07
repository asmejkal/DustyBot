using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.Commands
{
    public class ParameterRegistration
    {
        public string Name { get; set; }
        public string Format { get; set; }
        public ParameterType Type { get; set; }
        public ParameterFlags Flags { get; set; }
        public string Description { private get; set; }

        public string GetDescription(string prefix) => Description?.Replace(CommandRegistration.PrefixWildcard, prefix);
    }
}
