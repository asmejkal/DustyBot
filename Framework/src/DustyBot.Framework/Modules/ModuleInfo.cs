using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Discord;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Modules.Attributes;
using Microsoft.Extensions.Logging;
using ParameterInfo = DustyBot.Framework.Commands.ParameterInfo;

namespace DustyBot.Framework.Modules
{
    public class ModuleInfo
    {
        public Type Type { get; }
        public string Name { get; }
        public string Description { get; }
        public bool Hidden { get; }
        public IEnumerable<CommandInfo> Commands { get; }

        private ModuleInfo(Type type, string name, string description, bool hidden, IEnumerable<CommandInfo> commands)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Hidden = hidden;
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        internal static ModuleInfo Create<T>() => Create(typeof(T));

        internal static ModuleInfo Create(Type type)
        {
            var module = type.GetTypeInfo();

            var moduleAttribute = module.GetCustomAttribute<ModuleAttribute>();
            if (moduleAttribute == null)
                throw new ArgumentException($"Type {type} is not a module.");

            // Command attributes
            var commands = new List<CommandInfo>();
            foreach (var method in module.GetMethods())
            {
                var commandAttribute = method.GetCustomAttribute<CommandAttribute>();
                if (commandAttribute == null)
                    continue;

                // Required
                var handler = BuildCommandHandler(type, method);
                var primaryUsage = new CommandInfo.Usage(commandAttribute.InvokeString, commandAttribute.Verbs);
                var description = commandAttribute.Description;
                var flags = commandAttribute.Flags;

                // Optional
                List<ParameterInfo> parameters;
                if (method.GetCustomAttribute<IgnoreParametersAttribute>() != null)
                    parameters = new List<ParameterInfo>() { ParameterInfo.AlwaysMatching };
                else
                    parameters = method.GetCustomAttributes<ParameterAttribute>().Select(x => x.Registration).ToList();

                var userPermissions = new HashSet<GuildPermission>(method.GetCustomAttribute<PermissionsAttribute>()?.RequiredPermissions ?? Enumerable.Empty<GuildPermission>());
                var botPermissions = new HashSet<GuildPermission>(method.GetCustomAttribute<BotPermissionsAttribute>()?.RequiredPermissions ?? Enumerable.Empty<GuildPermission>());

                var examples = method.GetCustomAttributes<ExampleAttribute>().Select(x => x.Example).ToList();
                var comment = method.GetCustomAttribute<CommentAttribute>()?.Comment;
                var aliases = method.GetCustomAttributes<AliasAttribute>().Select(x => new CommandInfo.Usage(x.InvokeString, x.Verbs, x.Hidden)).ToList();

                commands.Add(new CommandInfo(type, handler, primaryUsage, aliases, userPermissions, botPermissions, parameters, description, examples, flags, comment));
            }

            return new ModuleInfo(type, moduleAttribute.Name, moduleAttribute.Description, moduleAttribute.Hidden, commands);
        }

        private static CommandInfo.CommandHandlerDelegate BuildCommandHandler(Type type, MethodInfo method)
        {
            var moduleParameter = Expression.Parameter(typeof(object));
            var convertedModuleParameter = Expression.Convert(moduleParameter, type);
            var commandParameter = Expression.Parameter(typeof(ICommand));
            var loggerParameter = Expression.Parameter(typeof(ILogger));
            var ctParameter = Expression.Parameter(typeof(CancellationToken));

            var parameters = method.GetParameters().Select(x => x.ParameterType).ToList();
            MethodCallExpression call;
            if (parameters.SequenceEqual(new[] { typeof(ICommand), typeof(ILogger), typeof(CancellationToken) }))
                call = Expression.Call(convertedModuleParameter, method, commandParameter, loggerParameter, ctParameter);
            if (parameters.SequenceEqual(new[] { typeof(ICommand), typeof(ILogger) }))
                call = Expression.Call(convertedModuleParameter, method, commandParameter, loggerParameter);
            else if (parameters.SequenceEqual(new[] { typeof(ICommand), typeof(CancellationToken) }))
                call = Expression.Call(convertedModuleParameter, method, commandParameter, ctParameter);
            else if (parameters.SequenceEqual(new[] { typeof(ICommand) }))
                call = Expression.Call(convertedModuleParameter, method, commandParameter);
            else
                throw new ArgumentException($"Invalid method signature of command {method.Name} on type {type}.");

            var lambda = Expression.Lambda<CommandInfo.CommandHandlerDelegate>(call, moduleParameter, commandParameter, loggerParameter, ctParameter);

            return lambda.Compile();
        }
    }
}
