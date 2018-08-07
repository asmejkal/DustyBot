using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public class CommandRegistration
    {
        public delegate Task CommandHandler(ICommand command);

        public const string PrefixWildcard = "{p}";

        public string InvokeString { get; set; }
        public string Verb { get; set; }
        public bool HasVerb => !string.IsNullOrEmpty(Verb);
        public string InvokeUsage => InvokeString + (HasVerb ? " " + Verb : string.Empty);

        public HashSet<Discord.GuildPermission> RequiredPermissions { get; set; } = new HashSet<Discord.GuildPermission>();
        public HashSet<Discord.GuildPermission> BotPermissions { get; set; } = new HashSet<Discord.GuildPermission>();

        public CommandHandler Handler { get; set; }

        public List<ParameterRegistration> Parameters { get; set; } = new List<ParameterRegistration>();

        public string Description { get; set; }

        public List<string> Examples { get; set; } = new List<string>();

        public string Comment { private get; set; }
        public string GetComment(string prefix) => Comment?.Replace(PrefixWildcard, prefix);

        public CommandFlags Flags { get; set; }
    }
}
