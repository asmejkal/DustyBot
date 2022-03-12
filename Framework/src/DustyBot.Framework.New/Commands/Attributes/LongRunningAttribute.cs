using System;

namespace DustyBot.Framework.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LongRunningAttribute : Attribute
    {
    }
}
