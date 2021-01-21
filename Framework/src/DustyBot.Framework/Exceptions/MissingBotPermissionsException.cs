using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Exceptions
{
    public class MissingBotPermissionsException : Exception
    {
        public MissingBotPermissionsException(params Discord.GuildPermission[] permissions) { Permissions.AddRange(permissions); }
        public MissingBotPermissionsException(string message, params Discord.GuildPermission[] permissions) : base(message) { Permissions.AddRange(permissions); }
        public MissingBotPermissionsException(string message, IEnumerable<Discord.GuildPermission> permissions, Exception inner) : base(message, inner) { Permissions.AddRange(permissions); }

        public List<Discord.GuildPermission> Permissions { get; } = new List<Discord.GuildPermission>();
    }
}
