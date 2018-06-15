using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Modules
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class ModuleAttribute : Attribute
    {
        public ModuleAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class RunAsyncAttribute : Attribute
    {
        public RunAsyncAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class CommandAttribute : Attribute
    {
        public CommandAttribute(string invokeString, string description)
        {
            InvokeString = invokeString;
            Description = description;
        }

        public string InvokeString { get; private set; }
        public string Description { get; private set; }
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
    public class ParametersAttribute : Attribute
    {
        public ParametersAttribute(params Commands.ParameterType[] requiredParameters)
        {
            RequiredParameters = requiredParameters;
        }

        public IEnumerable<Commands.ParameterType> RequiredParameters { get; private set; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class UsageAttribute : Attribute
    {
        public UsageAttribute(string usage)
        {
            Usage = usage;
        }

        public string Usage { get; private set; }
    }
}
