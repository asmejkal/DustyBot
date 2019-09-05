using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Modules
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class ModuleAttribute : Attribute
    {
        public ModuleAttribute(string name, string description, bool hidden = false)
        {
            Name = name;
            Description = description;
            Hidden = hidden;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool Hidden { get; }
    }

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

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class AliasAttribute : Attribute
    {
        public AliasAttribute(string invokeString)
            : this(invokeString, new List<string>())
        {
        }

        public AliasAttribute(string invokeString, string verb)
            : this(invokeString, new List<string>() { verb })
        {
        }

        public AliasAttribute(string invokeString, string verb1, string verb2)
            : this(invokeString, new List<string>() { verb1, verb2 })
        {
        }

        public AliasAttribute(string invokeString, string verb1, string verb2, string verb3)
            : this(invokeString, new List<string>() { verb1, verb2, verb3 })
        {
        }

        public AliasAttribute(string invokeString, string verb1, string verb2, string verb3, string verb4)
            : this(invokeString, new List<string>() { verb1, verb2, verb3, verb4 })
        {
        }

        public AliasAttribute(string invokeString, IEnumerable<string> verbs)
        {
            InvokeString = invokeString;
            Verbs = new List<string>(verbs);
        }

        public string InvokeString { get; }
        public List<string> Verbs { get; } = new List<string>();
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class ParameterAttribute : Attribute
    {
        #region basic

        public ParameterAttribute(string name, ParameterType type, string description = "")
            : this(name, null, type, ParameterFlags.None, description)
        {
        }

        public ParameterAttribute(string name, ParameterType type, ParameterFlags flags, string description = "")
            : this(name, null, type, flags, description)
        {
        }

        #endregion
        #region with format

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

        #endregion
        #region with format and inverse

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

        #endregion
        
        public ParameterAttribute(string name, string format, bool inverse, ParameterType type, ParameterFlags flags, string description = "")
        {
            Registration = new ParameterRegistration();

            Registration.Name = name;
            Registration.Format = format;
            Registration.Inverse = inverse;
            Registration.Type = type;
            Registration.Flags = flags;
            Registration.Description = description;
        }

        public ParameterRegistration Registration { get; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class IgnoreParametersAttribute : Attribute
    {
        public IgnoreParametersAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class CommentAttribute : Attribute
    {
        public CommentAttribute(string comment)
        {
            Comment = comment;
        }

        public string Comment { get; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class ExampleAttribute : Attribute
    {
        public ExampleAttribute(string example)
        {
            Example = example;
        }

        public string Example { get; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class PermissionsAttribute : Attribute
    {
        public PermissionsAttribute(params Discord.GuildPermission[] requiredPermissions)
        {
            RequiredPermissions = requiredPermissions;
        }
        
        public IEnumerable<Discord.GuildPermission> RequiredPermissions { get; private set; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class BotPermissionsAttribute : Attribute
    {
        public BotPermissionsAttribute(params Discord.GuildPermission[] requiredPermissions)
        {
            RequiredPermissions = requiredPermissions;
        }

        public IEnumerable<Discord.GuildPermission> RequiredPermissions { get; private set; }
    }
}
