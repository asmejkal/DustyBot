using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Exceptions
{
    public class MissingPermissionsException : Exception
    {
        public MissingPermissionsException(params Discord.GuildPermission[] permissions) : base("") { Permissions.AddRange(permissions); }
        public MissingPermissionsException(string message, params Discord.GuildPermission[] permissions) : base(message) { Permissions.AddRange(permissions); }
        public MissingPermissionsException(string message, IEnumerable<Discord.GuildPermission> permissions, Exception inner) : base(message, inner) { Permissions.AddRange(permissions); }

        public List<Discord.GuildPermission> Permissions { get; } = new List<Discord.GuildPermission>();
    }
}
