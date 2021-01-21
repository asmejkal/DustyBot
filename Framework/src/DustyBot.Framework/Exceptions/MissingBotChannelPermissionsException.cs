using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Exceptions
{
    public class MissingBotChannelPermissionsException : Exception
    {
        public MissingBotChannelPermissionsException(params Discord.ChannelPermission[] permissions) { Permissions.AddRange(permissions); }
        public MissingBotChannelPermissionsException(string message, params Discord.ChannelPermission[] permissions) : base(message) { Permissions.AddRange(permissions); }
        public MissingBotChannelPermissionsException(string message, IEnumerable<Discord.ChannelPermission> permissions, Exception inner) : base(message, inner) { Permissions.AddRange(permissions); }

        public List<Discord.ChannelPermission> Permissions { get; } = new List<Discord.ChannelPermission>();
    }
}
