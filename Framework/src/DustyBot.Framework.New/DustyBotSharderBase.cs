using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Text;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Async;
using DustyBot.Core.Comparers;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Commands.TypeParsers;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;
using Qmmands.Default;
using Qmmands.Text;
using Qommon.Collections;
using Qommon.Metadata;

namespace DustyBot.Framework
{
    public abstract class DustyBotSharderBase : DiscordBot
    {
        public DustyBotSharderBase(
            IOptions<DiscordBotConfiguration> options,
            ILogger<DiscordBot> logger,
            IServiceProvider services,
            DiscordClient client)
            : base(options, logger, services, client)
        {
        }

        public async Task<IEnumerable<ITextCommandMatch>> FindCommandsAsync(IGatewayUserMessage message)
        {
            // We check if the message is suitable for execution.
            // By default excludes bot messages.
            if (!await OnMessage(message).ConfigureAwait(false))
                return Enumerable.Empty<ITextCommandMatch>();

            // We get the prefixes from the prefix provider.
            var prefixes = await Prefixes.GetPrefixesAsync(message).ConfigureAwait(false);
            if (prefixes == null)
                return Enumerable.Empty<ITextCommandMatch>();

            // We try to find a prefix in the message.
            foreach (var prefix in prefixes)
            {
                if (prefix == null)
                    continue;

                if (prefix.TryFind(message, out var output))
                {
                    return Commands.GetCommandMapProvider().GetRequiredMap<ITextCommandMap>().FindMatches(output);
                }
            }

            return Enumerable.Empty<ITextCommandMatch>();
        }

        public override IDiscordTextCommandContext CreateTextCommandContext(IPrefix prefix, ReadOnlyMemory<char> input, IGatewayUserMessage message, IMessageGuildChannel? channel)
        {
            var context = base.CreateTextCommandContext(prefix, input, message, channel);
            context.SetMetadata(MetadataKeys.CorrelationId, Guid.NewGuid());
            return context;
        }

        protected override ValueTask InitializeModules(CancellationToken cancellationToken = default)
        {
            var types = Services.GetService<ModuleCollection>();
            if (types == null || !types.Any())
                return default;

            var modules = new List<IModule>();
            foreach (var type in types)
                modules.Add(Commands.AddModule(type.GetTypeInfo(), MutateModule));

            Logger.LogInformation("Added {ModuleCount} command modules with {CommandCount} commands.", modules.Count, modules.SelectMany(CommandUtilities.EnumerateAllCommands).Count());

            return default;
        }

        protected override void MutateModule(IModuleBuilder moduleBuilder)
        {
            if (moduleBuilder is ITextModuleBuilder textModuleBuilder)
            {
                ProcessDefaultAttributes(textModuleBuilder);
                ProcessRemarkAttributes(textModuleBuilder);
                ProcessVerbCommandAttributes(textModuleBuilder);
            }

            base.MutateModule(moduleBuilder);
        }

        protected override async ValueTask AddTypeParsers(DefaultTypeParserProvider typeParserProvider, CancellationToken cancellationToken)
        {
            await base.AddTypeParsers(typeParserProvider, cancellationToken);

            typeParserProvider.AddParser(new DateOnlyTypeParser());
            typeParserProvider.AddParser(new LocalEmbedTypeParser());
            typeParserProvider.AddParser(new TimeOnlyTypeParser());
            typeParserProvider.AddParser(new UriTypeParser());
            typeParserProvider.AddParser(new RestUserTypeParser());
            typeParserProvider.AddParser(new UserTypeParser());
            typeParserProvider.AddParser(new MatchTypeParser());
            typeParserProvider.AddParser(new GuidTypeParser());

            typeParserProvider.ReplaceParser(new MemberTypeParser());
        }

        protected override ValueTask<IResult> OnBeforeExecuted(IDiscordCommandContext context)
        {
            if (context is not IDiscordTextGuildCommandContext guildContext)
                return new(Results.Success);

            var guild = guildContext.Bot.GetGuild(guildContext.GuildId);
            var channel = guildContext.Channel;
            if (guild is not null && channel is not null && guild.GetBotPermissions(channel).HasFlag(Permissions.SendMessages))
                return new(Results.Success);
            else
                return new(Results.Failure("Can't send messages in the given channel"));
        }

        protected override ValueTask OnCommandResult(IDiscordCommandContext context, IDiscordCommandResult result)
        {
            if (context.Command is not null)
            {
                var logger = TryGetModuleType(context.Command.Module, out var type)
                ? Services.GetRequiredService<ILoggerFactory>().CreateLogger(type) : Logger;

                logger.WithCommandContext(context).LogInformation("Command completed with {CommandResult}", result.GetType().Name);
            }

            return base.OnCommandResult(context, result);
        }

        protected override ValueTask OnFailedResult(IDiscordCommandContext context, IResult result)
        {
            if (result is not CommandNotFoundResult && context is IDiscordTextCommandContext textContext && context.Command is not null)
            {
                var logger = TryGetModuleType(context.Command.Module, out var type)
                    ? Services.GetRequiredService<ILoggerFactory>().CreateLogger(type) : Logger;

                using var scope = logger.WithCommandUsageContext(textContext).BeginScope();
                if (context is IDiscordTextGuildCommandContext guildContext)
                {
                    logger.LogInformation("Command {MessageContent} failed with {CommandResult}", textContext.Message.Content, result.GetType().Name);

                    if (context.Command.HideInvocation())
                    {
                        var guild = guildContext.Bot.GetGuild(guildContext.GuildId);
                        var channel = guildContext.Channel;
                        if (guild is not null && channel is not null && guild.GetBotPermissions(channel).HasFlag(Permissions.ManageMessages))
                        {
                            TaskHelper.FireForget(() => guildContext.Message.DeleteAsync(cancellationToken: StoppingToken),
                                ex => Logger.LogError(ex, "Failed to hide failed command's invocation message"));
                        }
                    }
                }
                else
                {
                    logger.LogInformation("Command {MessageContentRedacted} failed with {CommandResult}", 
                        textContext.Prefix + string.Join(' ', textContext.Path ?? Enumerable.Empty<ReadOnlyMemory<char>>()),
                        result.GetType().Name);
                }
            }

            return base.OnFailedResult(context, result);
        }

        private static void ProcessDefaultAttributes(ITextModuleBuilder moduleBuilder)
        {
            // Skip the synthetic "verb" submodules ProcessVerbCommandAttributes builds below: their command
            // builders' MethodInfo/ParameterInfo belong to some other, real module - nothing here to
            // (re-)process for them (see the IsVerbModule doc comment for why TypeInfo can't be used here).
            if (moduleBuilder.GetMetadataOrDefault<bool>(MetadataKeys.IsVerbModule))
                return;

            var context = new NullabilityInfoContext();
            foreach (var command in moduleBuilder.Commands)
            {
                foreach (var parameter in command.Parameters)
                {
                    if (parameter.ParameterInfo is not ParameterInfo parameterInfo)
                        continue;

                    var attribute = parameter.CustomAttributes.OfType<DefaultAttribute>().FirstOrDefault();
                    if (attribute != null)
                    {
                        parameter.DefaultValue = attribute.DefaultValue;
                    }
                    else
                    {
                        var nullableInfo = context.Create(parameterInfo);
                        if (nullableInfo.ReadState == NullabilityState.Nullable)
                            parameter.CustomAttributes.Add(new DefaultAttribute(null));
                    }
                }
            }
        }

        private static void ProcessRemarkAttributes(ITextModuleBuilder moduleBuilder)
        {
            // See the comment in ProcessDefaultAttributes: skip synthetic modules, their commands already
            // had their remarks built once while still attached to their real, original module.
            if (moduleBuilder.GetMetadataOrDefault<bool>(MetadataKeys.IsVerbModule))
                return;

            static string Build(string? remarks, IEnumerable<Attribute> attributes)
            {
                var builder = new StringBuilder(remarks ?? "");
                foreach (var remark in attributes.OfType<RemarkAttribute>().Select(x => x.Remark))
                    builder.AppendLine(remark);

                return builder.ToString();
            }

            moduleBuilder.SetMetadata(MetadataKeys.Remarks, Build(moduleBuilder.GetMetadataOrDefault<string>(MetadataKeys.Remarks), moduleBuilder.CustomAttributes));
            foreach (var command in moduleBuilder.Commands)
            {
                command.SetMetadata(MetadataKeys.Remarks, Build(command.GetMetadataOrDefault<string>(MetadataKeys.Remarks), command.CustomAttributes));
            }
        }

        private static void ProcessVerbCommandAttributes(ITextModuleBuilder moduleBuilder)
        {
            // Critical, not just a fast path: without this, relocated commands' MethodInfo still points at
            // their original [VerbCommand]-decorated method, so re-running this against the synthetic
            // module they just got moved into would relocate them again into a brand new nested copy - and
            // since base.MutateModule visits every submodule it finds, including newly created ones, that
            // recurses forever (stack overflow) instead of terminating once the tree settles.
            if (moduleBuilder.GetMetadataOrDefault<bool>(MetadataKeys.IsVerbModule))
                return;

            var verbCommands = moduleBuilder.Commands
                .Select(x => (Command: x, Verbs: x.MethodInfo?.GetCustomAttribute<VerbCommandAttribute>()?.Verbs.ToList()))
                .Where(x => x.Verbs != null)
                .ToList();

            foreach (var verbCommand in verbCommands)
                moduleBuilder.Commands.Remove(verbCommand.Command);

            var comparer = new SequenceEqualityComparer<string>();
            var modules = new Dictionary<IEnumerable<string>, ITextModuleBuilder>(comparer)
            {
                { Enumerable.Empty<string>(), moduleBuilder }
            };

            for (var level = 0; verbCommands.Any(x => x.Verbs!.Count > level); ++level)
            {
                foreach (var group in verbCommands.Where(x => x.Verbs!.Count > level).GroupBy(x => x.Verbs!.Take(level + 1), comparer))
                {
                    var parent = modules[group.Key.SkipLast(1)];
                    var existing = parent.Submodules.FirstOrDefault(x => x.Aliases.Contains(group.Key.Last()));
                    var commands = group.Where(x => x.Verbs!.Count == level + 1).Select(x => x.Command);
                    if (existing == null)
                    {
                        // Must carry the source TypeInfo forward: command execution instantiates
                        // command.Module.TypeInfo to invoke the method on, and these commands' MethodInfo
                        // still belongs to moduleBuilder's own type, not some new synthetic one.
                        var newModuleBuilder = new TextModuleBuilder(parent, moduleBuilder.TypeInfo!);
                        newModuleBuilder.SetMetadata(MetadataKeys.IsVerbModule, true);
                        newModuleBuilder.Aliases.Add(group.Key.Last());
                        newModuleBuilder.Commands.AddRange(commands);
                        modules.Add(group.Key, newModuleBuilder);

                        parent.Submodules.Add(newModuleBuilder);
                    }
                    else
                    {
                        if (existing.Commands.SelectMany(x => x.Aliases).Intersect(commands.SelectMany(x => x.Aliases)).Any())
                            throw new InvalidOperationException($"An existing command alias overlaps with a verb command in module {existing.TypeInfo?.Name ?? existing.Name}");

                        existing.Commands.AddRange(commands);
                        modules.Add(group.Key, existing);
                    }
                }
            }
        }

        private static bool TryGetModuleType(IModule module, [NotNullWhen(true)] out Type? result)
        {
            while ((result = module.TypeInfo) == null && module.Parent != null)
                module = module.Parent;

            return result != null;
        }
    }
}
