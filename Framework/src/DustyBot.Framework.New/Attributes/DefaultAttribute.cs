using System;

namespace DustyBot.Framework.Attributes
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
