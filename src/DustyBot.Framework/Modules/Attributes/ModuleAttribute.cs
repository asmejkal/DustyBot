using System;

namespace DustyBot.Framework.Modules.Attributes
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
}
