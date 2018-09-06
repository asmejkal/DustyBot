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
            Hidden = moduleAttr.Hidden;

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
                    Verbs = commandAttr.Verbs,
                    Handler = (CommandRegistration.CommandHandler)method.CreateDelegate(typeof(CommandRegistration.CommandHandler), this),
                    Description = commandAttr.Description,
                    Flags = commandAttr.Flags
                };

                //Optional
                command.Parameters = method.GetCustomAttributes<ParameterAttribute>().Select(x => x.Registration).ToList();

                var permissions = method.GetCustomAttribute<PermissionsAttribute>();
                if (permissions != null)
                    command.RequiredPermissions = new HashSet<Discord.GuildPermission>(permissions.RequiredPermissions);

                var botPermissions = method.GetCustomAttribute<BotPermissionsAttribute>();
                if (botPermissions != null)
                    command.BotPermissions = new HashSet<Discord.GuildPermission>(botPermissions.RequiredPermissions);

                command.Examples = method.GetCustomAttributes<ExampleAttribute>().Select(x => x.Example).ToList();
                command.Comment = method.GetCustomAttribute<CommentAttribute>()?.Comment;

                handledCommandsList.Add(command);
            }

            HandledCommands = handledCommandsList;
        }
    }
}
