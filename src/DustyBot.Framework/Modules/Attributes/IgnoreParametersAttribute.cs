using System;

namespace DustyBot.Framework.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class IgnoreParametersAttribute : Attribute
    {
        public IgnoreParametersAttribute()
        {
        }
    }
}
