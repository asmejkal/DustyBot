using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.Commands
{
    public class ParameterRegistration
    {
        ParameterFlags _flags;

        public string Name { get; set; }
        public string Format { get; set; }
        public bool Inverse { get; set; }
        public ParameterType Type { get; set; }
        public string Description { private get; set; }
        public ParameterFlags Flags
        {
            get
            {
                return _flags;
            }
            set
            {
                if (value.HasFlag(ParameterFlags.Repeatable) && value.HasFlag(ParameterFlags.Remainder))
                    throw new ArgumentException("Invalid flag combination.");

                _flags = value;
            }
        }

        public ParameterRegistration()
        {
        }

        public ParameterRegistration(ParameterRegistration o)
        {
            _flags = o._flags;
            Name = o.Name;
            Format = o.Format;
            Type = o.Type;
            Description = o.Description;
        }

        public string GetDescription(string prefix) => Description?.Replace(CommandRegistration.PrefixWildcard, prefix);
    }
}
