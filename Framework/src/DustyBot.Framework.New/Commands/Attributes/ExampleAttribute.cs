    using System;

namespace DustyBot.Framework.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ExampleAttribute : Attribute
    {
        public string Example { get; }

        public ExampleAttribute(string example)
        {
            Example = example;
        }
    }
}
