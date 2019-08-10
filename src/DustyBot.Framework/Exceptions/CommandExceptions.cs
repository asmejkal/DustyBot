using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Exceptions
{
    public class CommandException : Exception
    {
        public CommandException() { }
        public CommandException(string message) : base(message) { }
        public CommandException(string message, Exception inner) : base(message, inner) { }
    }

    public class IncorrectParametersCommandException : CommandException
    {
        public IncorrectParametersCommandException(bool showUsage = true) { ShowUsage = showUsage; }
        public IncorrectParametersCommandException(string message, bool showUsage = true) : base(message) { ShowUsage = showUsage; }
        public IncorrectParametersCommandException(string message, Exception inner, bool showUsage = true) : base(message, inner) { ShowUsage = showUsage; }

        public bool ShowUsage { get; }
    }

    public class UnclearParametersCommandException : CommandException
    {
        public UnclearParametersCommandException(string message, bool showUsage = true) : base(message) { ShowUsage = showUsage; }
        public UnclearParametersCommandException(string message, Exception inner, bool showUsage = true) : base(message, inner) { ShowUsage = showUsage; }

        public bool ShowUsage { get; }
    }

    public class MissingPermissionsException : Exception
    {
        public MissingPermissionsException(params Discord.GuildPermission[] permissions) : base("") { Permissions.AddRange(permissions); }
        public MissingPermissionsException(string message, params Discord.GuildPermission[] permissions) : base(message) { Permissions.AddRange(permissions); }
        public MissingPermissionsException(string message, IEnumerable<Discord.GuildPermission> permissions, Exception inner) : base(message, inner) { Permissions.AddRange(permissions); }

        public List<Discord.GuildPermission> Permissions { get; } = new List<Discord.GuildPermission>();
    }

    public class MissingBotPermissionsException : Exception
    {
        public MissingBotPermissionsException(params Discord.GuildPermission[] permissions) { Permissions.AddRange(permissions); }
        public MissingBotPermissionsException(string message, params Discord.GuildPermission[] permissions) : base(message) { Permissions.AddRange(permissions); }
        public MissingBotPermissionsException(string message, IEnumerable<Discord.GuildPermission> permissions, Exception inner) : base(message, inner) { Permissions.AddRange(permissions); }

        public List<Discord.GuildPermission> Permissions { get; } = new List<Discord.GuildPermission>();
    }

    public class MissingBotChannelPermissionsException : Exception
    {
        public MissingBotChannelPermissionsException(params Discord.ChannelPermission[] permissions) { Permissions.AddRange(permissions); }
        public MissingBotChannelPermissionsException(string message, params Discord.ChannelPermission[] permissions) : base(message) { Permissions.AddRange(permissions); }
        public MissingBotChannelPermissionsException(string message, IEnumerable<Discord.ChannelPermission> permissions, Exception inner) : base(message, inner) { Permissions.AddRange(permissions); }

        public List<Discord.ChannelPermission> Permissions { get; } = new List<Discord.ChannelPermission>();
    }
}
