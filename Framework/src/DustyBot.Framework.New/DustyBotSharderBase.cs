using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Sharding;
using Disqord.Gateway;
using Disqord.Rest;
using Disqord.Sharding;
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

namespace DustyBot.Framework
{
    public abstract class DustyBotSharderBase : DiscordBotSharder
    {
        public DustyBotSharderBase(
            IOptions<DiscordBotSharderConfiguration> options,
            ILogger<DiscordBotSharder> logger,
            IServiceProvider services,
            DiscordClientSharder client)
            : base(options, logger, services, client)
        {
        }

        public async Task<IEnumerable<CommandMatch>> FindCommandsAsync(IGatewayUserMessage message)
        {
            // We check if the message is suitable for execution.
            // By default excludes bot messages.
            if (!await CheckMessageAsync(message).ConfigureAwait(false))
                return Enumerable.Empty<CommandMatch>();

            // We get the prefixes from the prefix provider.
            var prefixes = await Prefixes.GetPrefixesAsync(message).ConfigureAwait(false);
            if (prefixes == null)
                return Enumerable.Empty<CommandMatch>();

            // We try to find a prefix in the message.
            IPrefix? foundPrefix = null;
            string? output = null;
            foreach (var prefix in prefixes)
            {
                if (prefix == null)
                    continue;

                if (prefix.TryFind(message, out output))
                {
                    foundPrefix = prefix;
                    break;
                }
            }

            if (foundPrefix == null)
                return Enumerable.Empty<CommandMatch>();

            return Commands.FindCommands(output);
        }

        public override DiscordCommandContext CreateCommandContext(IPrefix prefix, string input, IGatewayUserMessage message, CachedMessageGuildChannel channel)
        {
            var scope = Services.CreateScope();
            DiscordCommandContext context = message.GuildId != null
                ? new DustyGuildCommandContext(this, prefix, input, message, channel, scope)
                : new DustyCommandContext(this, prefix, input, message, scope);

            context.Services.GetRequiredService<ICommandContextAccessor>().Context = context;
            return context;
        }

        protected override ValueTask AddModulesAsync(CancellationToken cancellationToken = default)
        {
            var types = Services.GetService<ModuleCollection>();
            if (types == null || !types.Any())
                return default;

            try
            {
                var modules = new List<Qmmands.Module>();
                foreach (var type in types)
                    modules.Add(Commands.AddModule(type, MutateModule));

                Logger.LogInformation("Added {ModuleCount} command modules with {CommandCount} commands.", modules.Count, modules.SelectMany(CommandUtilities.EnumerateAllCommands).Count());
            }
            catch (CommandMappingException ex)
            {
                Logger.LogCritical(ex, "Failed to map command {Command} in module {Module}:", ex.Command, ex.Command.Module);
                throw;
            }

            return default;
        }

        protected override void MutateModule(ModuleBuilder moduleBuilder)
        {
            ProcessDefaultAttributes(moduleBuilder);
            ProcessRemarkAttributes(moduleBuilder);
            ProcessVerbCommandAttributes(moduleBuilder);

            base.MutateModule(moduleBuilder);
        }

        protected override async ValueTask AddTypeParsersAsync(CancellationToken cancellationToken = default)
        {
            await base.AddTypeParsersAsync(cancellationToken);

            Commands.AddTypeParser(new DateOnlyTypeParser());
            Commands.AddTypeParser(new LocalEmbedTypeParser());
            Commands.AddTypeParser(new TimeOnlyTypeParser());
            Commands.AddTypeParser(new UriTypeParser());
            Commands.AddTypeParser(new RestUserTypeParser());
            Commands.AddTypeParser(new UserTypeParser());

            Commands.ReplaceTypeParser(new MemberTypeParser());
        }

        protected override ValueTask<bool> BeforeExecutedAsync(DiscordCommandContext context)
        {
            if (context is not DiscordGuildCommandContext guildContext)
                return new(true);

            return new(guildContext.Guild.GetBotPermissions(guildContext.Channel).SendMessages);
        }

        protected override ValueTask HandleCommandResultAsync(DiscordCommandContext context, DiscordCommandResult result)
        {
            var logger = TryGetModuleType(context.Command.Module, out var type) 
                ? Services.GetRequiredService<ILoggerFactory>().CreateLogger(type) : Logger;

            logger.WithCommandContext(context).LogInformation("Command completed with {CommandResult}", result.GetType().Name);

            return base.HandleCommandResultAsync(context, result);
        }

        protected override ValueTask HandleFailedResultAsync(DiscordCommandContext context, FailedResult result)
        {
            if (result is not CommandNotFoundResult)
            {
                var logger = TryGetModuleType(context.Command.Module, out var type)
                    ? Services.GetRequiredService<ILoggerFactory>().CreateLogger(type) : Logger;

                using var scope = logger.WithCommandUsageContext(context).BeginScope();
                if (context is DiscordGuildCommandContext guildContext)
                {
                    logger.LogInformation("Command {MessageContent} failed with {CommandResult}", context.Message.Content, result.GetType().Name);

                    if (guildContext.Command.Attributes.OfType<HideInvocationAttribute>().Any()
                        && guildContext.Guild.GetBotPermissions(guildContext.Channel).ManageMessages)
                    {
                        TaskHelper.FireForget(() => context.Message.DeleteAsync(cancellationToken: StoppingToken),
                            ex => Logger.LogError(ex, "Failed to hide failed command's invocation message"));
                    }
                }
                else
                {
                    logger.LogInformation("Command {MessageContentRedacted} failed with {CommandResult}", context.Prefix.ToString() + string.Join(' ', context.Path), result.GetType().Name);
                }
            }

            return base.HandleFailedResultAsync(context, result);
        }

        private static void ProcessDefaultAttributes(ModuleBuilder moduleBuilder)
        {
            foreach (var submodule in moduleBuilder.Submodules)
                ProcessDefaultAttributes(submodule);

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
            foreach (var submodule in moduleBuilder.Submodules)
                ProcessRemarkAttributes(submodule);

            static string Build(string remarks, IEnumerable<Attribute> attributes)
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
            foreach (var submodule in moduleBuilder.Submodules)
                ProcessVerbCommandAttributes(submodule);

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
                    var parent = modules[group.Key.SkipLast(1)];
                    var existing = parent.Submodules.FirstOrDefault(x => x.Aliases.Contains(group.Key.Last()));
                    var commands = group.Where(x => x.Verbs!.Count == level + 1).Select(x => x.Command);
                    if (existing == null)
                    {
                        parent.AddSubmodule(x =>
                        {
                            x.AddAlias(group.Key.Last());
                            x.Commands.AddRange(commands);
                            modules.Add(group.Key, x);
                        });
                    }
                    else
                    {
                        if (existing.Commands.SelectMany(x => x.Aliases).Intersect(commands.SelectMany(x => x.Aliases)).Any())
                            throw new InvalidOperationException($"An existing command alias overlaps with a verb command in module {existing.Type?.Name ?? existing.Name}");

                        existing.Commands.AddRange(commands);
                        modules.Add(group.Key, existing);
                    }
                }
            }
        }

        private static bool TryGetModuleType(Qmmands.Module module, out Type result)
        {
            while ((result = module.Type) == null && module.Parent != null)
                module = module.Parent;

            return result != null;
        }
    }
}
