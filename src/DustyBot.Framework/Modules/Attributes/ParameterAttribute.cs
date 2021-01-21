using System;
using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class ParameterAttribute : Attribute
    {
        public ParameterAttribute(string name, ParameterType type, string description = "")
            : this(name, null, type, ParameterFlags.None, description)
        {
        }

        public ParameterAttribute(string name, ParameterType type, ParameterFlags flags, string description = "")
            : this(name, null, type, flags, description)
        {
        }

        public ParameterAttribute(string name, string format, string description = "")
            : this(name, format, ParameterType.String, ParameterFlags.None, description)
        {
        }

        public ParameterAttribute(string name, string format, ParameterFlags flags, string description = "")
            : this(name, format, ParameterType.String, flags, description)
        {
        }

        public ParameterAttribute(string name, string format, ParameterType type, string description = "")
            : this(name, format, type, ParameterFlags.None, description)
        {
        }

        public ParameterAttribute(string name, string format, ParameterType type, ParameterFlags flags, string description = "")
            : this(name, format, false, type, flags, description)
        {
        }

        public ParameterAttribute(string name, string format, bool inverse, string description = "")
            : this(name, format, inverse, ParameterType.String, ParameterFlags.None, description)
        {
        }

        public ParameterAttribute(string name, string format, bool inverse, ParameterFlags flags, string description = "")
            : this(name, format, inverse, ParameterType.String, flags, description)
        {
        }

        public ParameterAttribute(string name, string format, bool inverse, ParameterType type, string description = "")
            : this(name, format, inverse, type, ParameterFlags.None, description)
        {
        }
        
        public ParameterAttribute(string name, string format, bool inverse, ParameterType type, ParameterFlags flags, string description = "")
        {
            Registration = new ParameterInfo();

            Registration.Name = name;
            Registration.Format = format;
            Registration.Inverse = inverse;
            Registration.Type = type;
            Registration.Flags = flags;
            Registration.Description = description;
        }

        public ParameterInfo Registration { get; }
    }
}
