using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class AliasAttribute : Attribute
    {
        public AliasAttribute(string invokeString, bool hidden = false)
            : this(invokeString, new List<string>(), hidden)
        {
        }

        public AliasAttribute(string invokeString, string verb, bool hidden = false)
            : this(invokeString, new List<string>() { verb }, hidden)
        {
        }

        public AliasAttribute(string invokeString, string verb1, string verb2, bool hidden = false)
            : this(invokeString, new List<string>() { verb1, verb2 }, hidden)
        {
        }

        public AliasAttribute(string invokeString, string verb1, string verb2, string verb3, bool hidden = false)
            : this(invokeString, new List<string>() { verb1, verb2, verb3 }, hidden)
        {
        }

        public AliasAttribute(string invokeString, string verb1, string verb2, string verb3, string verb4, bool hidden = false)
            : this(invokeString, new List<string>() { verb1, verb2, verb3, verb4 }, hidden)
        {
        }

        public AliasAttribute(string invokeString, IEnumerable<string> verbs, bool hidden = false)
        {
            InvokeString = invokeString;
            Verbs = new List<string>(verbs);
            Hidden = hidden;
        }

        public string InvokeString { get; }
        public List<string> Verbs { get; } = new List<string>();
        public bool Hidden { get; }
    }
}
