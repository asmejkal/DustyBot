using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System.Reflection;

namespace DustyBot.Modules
{
    [Module("Self", "Help and bot-related commands.")]
    class SelfModule : Framework.Modules.Module
    {
        public ICommunicator Communicator { get; private set; }
        public IOwnerConfig Config { get; private set; }
        public IModuleCollection ModuleCollection { get; private set; }

        public SelfModule(ICommunicator communicator, Settings.IOwnerConfig config, IModuleCollection moduleCollection)
        {
            Communicator = communicator;
            Config = config;
            ModuleCollection = moduleCollection;
        }

        [Command("help", "Prints usage info.")]
        [Usage("Use without parameters to see a list of modules and commands. Type `{p}help CommandName` to see usage and help for a specific command.")]
        public async Task Help(ICommand command)
        {
            if (command.ParametersCount <= 0)
            {
                var embed = new EmbedBuilder()
                .WithTitle("List of modules")
                .WithFooter($"Type `{Config.CommandPrefix}help CommandName` to see usage and help for a specific command.");

                foreach (var module in ModuleCollection.Modules)
                {
                    var description = module.Description + "\n";
                    foreach (var handledCommand in module.HandledCommands)
                        description += "\n**" + Config.CommandPrefix + handledCommand.InvokeString + "** – " + handledCommand.Description + "";
                    
                    embed.AddField(x => x.WithName(":pushpin: " + module.Name).WithValue(description));
                }
                
                await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
            }
            else
            {
                //Try to find the command
                CommandRegistration commandRegistration = null;
                string searchedCommand = command.Body.StartsWith(Config.CommandPrefix) ? command.Body.Substring(Config.CommandPrefix.Length) : command.Body;
                foreach (var module in ModuleCollection.Modules)
                {
                    foreach (var handledCommand in module.HandledCommands)
                    {
                        if (string.Equals(handledCommand.InvokeString, searchedCommand, StringComparison.CurrentCultureIgnoreCase))
                            commandRegistration = handledCommand;
                    }
                }

                if (commandRegistration == null)
                    throw new Framework.Exceptions.IncorrectParametersCommandException("This is not a recognized command.");

                //Build response
                var embed = new EmbedBuilder()
                    .WithTitle($"Command {Config.CommandPrefix}{commandRegistration.InvokeString}")
                    .WithDescription(commandRegistration.Description);

                if (!string.IsNullOrEmpty(commandRegistration.GetUsage(Config.CommandPrefix)))
                    embed.AddField(x => x.WithName("Usage").WithValue(commandRegistration.GetUsage(Config.CommandPrefix)));

                await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
            }
        }

        [Command("about", "Bot and version information.")]
        public async Task About(ICommand command)
        {
            var embed = new EmbedBuilder()
                .WithTitle("DustyBot v" + typeof(Bot).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version)
                .AddInlineField("Author", "Yebafan#3517")
                .AddInlineField("Owners", string.Join("\n", Config.OwnerIDs))
                .AddInlineField("Framework", "v" + typeof(Framework.Framework).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version)
                .AddInlineField("GitHub", "https://github.com/yebafan/DustyBot");

            await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }
    }
}
