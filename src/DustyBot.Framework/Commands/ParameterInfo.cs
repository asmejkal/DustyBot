using System;

namespace DustyBot.Framework.Commands
{
    internal class ParameterInfo
    {
        public static readonly ParameterInfo AlwaysMatching = new ParameterInfo()
        {
            Type = ParameterType.String,
            Flags = ParameterFlags.Remainder | ParameterFlags.Optional
        };

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

        private ParameterFlags _flags;

        public ParameterInfo()
        {
        }

        public ParameterInfo(ParameterInfo o)
        {
            _flags = o._flags;
            Name = o.Name;
            Format = o.Format;
            Type = o.Type;
            Description = o.Description;
        }

        public string GetDescription(string prefix) => Description?.Replace(CommandInfo.PrefixWildcard, prefix);
    }
}
