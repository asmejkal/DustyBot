using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class DateFormatsAttribute : Attribute
    {
        public IReadOnlyList<string> Formats { get; }

        public DateFormatsAttribute(params string[] formats)
        {
            Formats = formats;
        }
    }
}
