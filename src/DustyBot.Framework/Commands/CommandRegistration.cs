using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public class CommandRegistration
    {
        public class Usage
        {
            public string InvokeString { get; set; }
            public List<string> Verbs { get; set; }
            public bool HasVerbs => Verbs.Count > 0;
            public string InvokeUsage => InvokeString + (HasVerbs ? " " + string.Join(" ", Verbs) : string.Empty);

            public Usage(string invokeString, List<string> verbs)
            {
                InvokeString = invokeString;
                Verbs = verbs;
            }
        }

        public delegate Task CommandHandler(ICommand command);

        public const string PrefixWildcard = "{p}";

        public Usage PrimaryUsage { get; set; }
        public List<Usage> Aliases { get; set; } = new List<Usage>();
        public IEnumerable<Usage> EveryUsage => new[] { PrimaryUsage }.Concat(Aliases);

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
