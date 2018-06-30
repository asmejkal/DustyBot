using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;

namespace DustyBot.Framework.Modules
{
    public abstract class Module : Events.EventHandler, IModule
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool Hidden { get; private set; }

        public IEnumerable<CommandRegistration> HandledCommands { get; private set; }

        public Module()
        {
            var module = GetType().GetTypeInfo();

            //Module attributes
            var moduleAttr =  module.GetCustomAttribute<ModuleAttribute>();
            if (moduleAttr == null)
                throw new InvalidOperationException("A module derived from Module must use the Module attribute.");

            Name = moduleAttr.Name;
            Description = moduleAttr.Description;
            Hidden = module.GetCustomAttribute<HiddenAttribute>() != null;

            //Command attributes
            var handledCommandsList = new List<CommandRegistration>();
            foreach (var method in module.GetMethods())
            {
                var commandAttr = method.GetCustomAttribute<CommandAttribute>();
                if (commandAttr == null)
                    continue;

                //Required
                var command = new CommandRegistration
                {
                    InvokeString = commandAttr.InvokeString,
                    Verb = commandAttr.Verb,
                    Handler = (CommandRegistration.CommandHandler)method.CreateDelegate(typeof(CommandRegistration.CommandHandler), this),
                    Description = commandAttr.Description
                };

                //Optional
                var parameters = method.GetCustomAttribute<ParametersAttribute>();
                if (parameters != null)
                    command.RequiredParameters = new List<ParameterType>(parameters.RequiredParameters);

                var permissions = method.GetCustomAttribute<PermissionsAttribute>();
                if (permissions != null)
                    command.RequiredPermissions = new HashSet<Discord.GuildPermission>(permissions.RequiredPermissions);

                var usage = method.GetCustomAttribute<UsageAttribute>();
                if (usage != null)
                    command.Usage = usage.Usage;

                var runAsync = method.GetCustomAttribute<RunAsyncAttribute>();
                command.RunAsync = runAsync != null;

                var ownerOnly = method.GetCustomAttribute<OwnerOnlyAttribute>();
                command.OwnerOnly = ownerOnly != null;

                var hidden = method.GetCustomAttribute<HiddenAttribute>();
                command.Hidden = hidden != null;

                handledCommandsList.Add(command);
            }

            HandledCommands = handledCommandsList;
        }
    }
}
