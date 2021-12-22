using System;

namespace DustyBot.Framework.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class DefaultAttribute : Attribute
    {
        public object? DefaultValue { get; }

        public DefaultAttribute(object? defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }
}
