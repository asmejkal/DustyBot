using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public class CommandRegistration
    {
        public const string PrefixWildcard = "{p}";

        public delegate Task CommandHandler(ICommand command);

        public string InvokeString { get; set; }
        public string Verb { get; set; }
        public bool HasVerb => !string.IsNullOrEmpty(Verb);

        public HashSet<Discord.GuildPermission> RequiredPermissions { get; set; } = new HashSet<Discord.GuildPermission>();
        public HashSet<Discord.GuildPermission> BotPermissions { get; set; } = new HashSet<Discord.GuildPermission>();

        public CommandHandler Handler { get; set; }

        public List<ParameterType> RequiredParameters { get; set; } = new List<ParameterType>();

        public string Description { get; set; }
        
        public string Usage { private get; set; }
        public string GetUsage(string prefix) => Usage?.Replace(PrefixWildcard, prefix);

        public bool RunAsync { get; set; }
        public bool OwnerOnly { get; set; }
        public bool Hidden { get; set; }
        public bool DirectMessageOnly { get; set; }
        bool _directMessageAllow;
        public bool DirectMessageAllow { get { return _directMessageAllow || DirectMessageOnly; } set { _directMessageAllow = value; } }
        public bool TypingIndicator { get; set; }
    }
}
