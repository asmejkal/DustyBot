using System;
using System.Collections.Generic;
using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class CommandAttribute : Attribute
    {
        public CommandAttribute(string invokeString, string description, CommandFlags flags = CommandFlags.None)
            : this(invokeString, new List<string>(), description, flags)
        {
        }

        public CommandAttribute(string invokeString, string verb, string description, CommandFlags flags = CommandFlags.None)
            : this(invokeString, new List<string>() { verb }, description, flags)
        {
        }

        public CommandAttribute(string invokeString, string verb1, string verb2, string description, CommandFlags flags = CommandFlags.None)
            : this(invokeString, new List<string>() { verb1, verb2 }, description, flags)
        {
        }

        public CommandAttribute(string invokeString, string verb1, string verb2, string verb3, string description, CommandFlags flags = CommandFlags.None)
            : this(invokeString, new List<string>() { verb1, verb2, verb3 }, description, flags)
        {
        }

        public CommandAttribute(string invokeString, string verb1, string verb2, string verb3, string verb4, string description, CommandFlags flags = CommandFlags.None)
            : this(invokeString, new List<string>() { verb1, verb2, verb3, verb4 }, description, flags)
        {
        }

        public CommandAttribute(string invokeString, IEnumerable<string> verbs, string description, CommandFlags flags = CommandFlags.None)
        {
            InvokeString = invokeString;
            Verbs = new List<string>(verbs);
            Description = description;
            Flags = flags;
        }

        public string InvokeString { get; }
        public List<string> Verbs { get; } = new List<string>();
        public string Description { get; }
        public CommandFlags Flags { get; }
    }
}
