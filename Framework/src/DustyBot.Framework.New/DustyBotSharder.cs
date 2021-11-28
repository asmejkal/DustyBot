using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord.Bot.Sharding;
using Disqord.Sharding;
using DustyBot.Core.Comparers;
using DustyBot.Framework.Attributes;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.TypeParsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;

namespace DustyBot.Framework
{
    public class DustyBotSharder : DiscordBotSharder
    {
        public DustyBotSharder(
            IOptions<DiscordBotSharderConfiguration> options,
            ILogger<DiscordBotSharder> logger,
            IServiceProvider services,
            DiscordClientSharder client)
            : base(options, logger, services, client)
        { 
        }

        protected override void MutateModule(ModuleBuilder moduleBuilder)
        {
            ProcessDefaultAttributes(moduleBuilder);
            ProcessRemarkAttributes(moduleBuilder);
            ProcessVerbCommandAttributes(moduleBuilder);

            base.MutateModule(moduleBuilder);
        }

        protected override ValueTask AddTypeParsersAsync(CancellationToken cancellationToken = default)
        {
            base.AddTypeParsersAsync(cancellationToken);

            Commands.AddTypeParser(new DateOnlyTypeParser());
            Commands.AddTypeParser(new LocalEmbedTypeParser());
            Commands.AddTypeParser(new TimeOnlyTypeParser());
            Commands.AddTypeParser(new UriTypeParser());
            Commands.AddTypeParser(new RestUserTypeParser());

            Commands.ReplaceTypeParser(new RestMemberTypeParser());

            return default;
        }

        private static void ProcessDefaultAttributes(ModuleBuilder moduleBuilder)
        {
            var context = new NullabilityInfoContext();
            var commandInfos = moduleBuilder.Type.GetTypeInfo().DeclaredMethods.Where(x => x.GetCustomAttribute<CommandAttribute>() != null);

            foreach (var (command, commandInfo) in moduleBuilder.Commands.Zip(commandInfos))
            {
                var parameterInfos = commandInfo.GetParameters();
                foreach (var (parameter, parameterInfo) in command.Parameters.Zip(parameterInfos))
                {
                    var attribute = parameter.Attributes.OfType<DefaultAttribute>().FirstOrDefault();
                    if (attribute != null)
                    {
                        parameter.DefaultValue = attribute.DefaultValue;
                    }
                    else
                    {
                        var nullableInfo = context.Create(parameterInfo);
                        if (nullableInfo.ReadState == NullabilityState.Nullable)
                            parameter.AddAttribute(new DefaultAttribute(null));
                    }
                }
            }
        }

        private static void ProcessRemarkAttributes(ModuleBuilder moduleBuilder)
        {
            string Build(string remarks, IEnumerable<Attribute> attributes)
            {
                var builder = new StringBuilder(remarks);
                foreach (var remark in attributes.OfType<RemarkAttribute>().Select(x => x.Remark))
                    builder.AppendLine(remark);

                return builder.ToString();
            }

            moduleBuilder.Remarks = Build(moduleBuilder.Remarks, moduleBuilder.Attributes);
            foreach (var command in moduleBuilder.Commands)
            {
                command.Remarks = Build(command.Remarks, command.Attributes);
                foreach (var parameter in command.Parameters)
                    parameter.Remarks = Build(parameter.Remarks, parameter.Attributes);
            }
        }

        private static void ProcessVerbCommandAttributes(ModuleBuilder moduleBuilder)
        {
            var commandInfos = moduleBuilder.Type.GetTypeInfo().DeclaredMethods.Where(x => x.GetCustomAttribute<CommandAttribute>() != null);
            var verbCommands = moduleBuilder.Commands
                .Zip(commandInfos)
                .Select(x => (Command: x.First, Verbs: x.Second.GetCustomAttribute<VerbCommandAttribute>()?.Verbs.ToList()))
                .Where(x => x.Verbs != null)
                .ToList();

            foreach (var verbCommand in verbCommands)
                moduleBuilder.Commands.Remove(verbCommand.Command);

            var comparer = new SequenceEqualityComparer<string>();
            var modules = new Dictionary<IEnumerable<string>, ModuleBuilder>(comparer)
            {
                { Enumerable.Empty<string>(), moduleBuilder }
            };

            for (var level = 0; verbCommands.Any(x => x.Verbs!.Count > level); ++level)
            {
                foreach (var group in verbCommands.Where(x => x.Verbs!.Count > level).GroupBy(x => x.Verbs!.Take(level + 1), comparer))
                {
                    modules[group.Key.SkipLast(1)].AddSubmodule(x =>
                    {
                        x.Commands.AddRange(group.Where(x => x.Verbs!.Count == level + 1).Select(x => x.Command));
                        x.AddAlias(group.Key.Last());

                        modules.Add(group.Key, x);
                    });
                }
            }
        }
    }
}
