using System;

namespace DustyBot.Framework.Commands.Attributes
{
    /// <summary>
    /// Marks a command or parameter that should be hidden from the help listing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class HiddenAttribute : Attribute
    {
        public HiddenAttribute()
        {
        }
    }
}
