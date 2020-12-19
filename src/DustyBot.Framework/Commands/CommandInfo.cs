using Discord;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public class CommandInfo
    {
        public class Usage
        {
            public string InvokeString { get; set; }
            public List<string> Verbs { get; set; }
            public bool HasVerbs => Verbs.Count > 0;
            public string InvokeUsage => InvokeString + (HasVerbs ? " " + string.Join(" ", Verbs) : string.Empty);
            public bool Hidden { get; set; }

            public Usage(string invokeString, List<string> verbs, bool hidden = false)
            {
                InvokeString = invokeString;
                Verbs = verbs;
                Hidden = hidden;
            }
        }

        public delegate Task CommandHandlerDelegate(object module, ICommand command, ILogger logger);

        public const string PrefixWildcard = "{p}";

        public Type ModuleType { get; }
        public CommandHandlerDelegate Handler { get; }

        public Usage PrimaryUsage { get; }
        public IReadOnlyCollection<Usage> Aliases { get; } = new List<Usage>();
        public IEnumerable<Usage> EveryUsage => new[] { PrimaryUsage }.Concat(Aliases);

        public IReadOnlyCollection<GuildPermission> UserPermissions { get; } = new HashSet<GuildPermission>();
        public IReadOnlyCollection<GuildPermission> BotPermissions { get; } = new HashSet<GuildPermission>();

        public IReadOnlyCollection<ParameterInfo> Parameters { get; } = new List<ParameterInfo>();

        public string Description { get; }

        public IReadOnlyCollection<string> Examples { get; } = new List<string>();

        public CommandFlags Flags { get; }

        private readonly string _comment;

        public CommandInfo(
            Type moduleType,
            CommandHandlerDelegate handler, 
            Usage primaryUsage, 
            IReadOnlyCollection<Usage> aliases, 
            IReadOnlyCollection<GuildPermission> userPermissions, 
            IReadOnlyCollection<GuildPermission> botPermissions, 
            IReadOnlyCollection<ParameterInfo> parameters, 
            string description, 
            IReadOnlyCollection<string> examples, 
            CommandFlags flags, 
            string comment)
        {
            ModuleType = moduleType ?? throw new ArgumentNullException(nameof(moduleType));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            PrimaryUsage = primaryUsage ?? throw new ArgumentNullException(nameof(primaryUsage));
            Aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
            UserPermissions = userPermissions ?? throw new ArgumentNullException(nameof(userPermissions));
            BotPermissions = botPermissions ?? throw new ArgumentNullException(nameof(botPermissions));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Examples = examples ?? throw new ArgumentNullException(nameof(examples));
            Flags = flags;
            _comment = comment;
        }

        public string GetComment(string prefix) => _comment?.Replace(PrefixWildcard, prefix);
    }
}
