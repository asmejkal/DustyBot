using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Modules.Attributes
{
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
