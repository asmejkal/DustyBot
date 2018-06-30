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
using Discord.WebSocket;

namespace DustyBot.Modules
{
    [Module("Self", "Help and bot-related commands.")]
    class SelfModule : Framework.Modules.Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public IModuleCollection ModuleCollection { get; private set; }
        public IDiscordClient Client { get; private set; }

        public SelfModule(ICommunicator communicator, ISettingsProvider settings, IModuleCollection moduleCollection, IDiscordClient client)
        {
            Communicator = communicator;
            Settings = settings;
            ModuleCollection = moduleCollection;
            Client = client;
        }

        [Command("help", "Prints usage info.")]
        [Usage("Use without parameters to see a list of modules and commands. Type `{p}help CommandName` to see usage and help for a specific command.")]
        public async Task Help(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();
            if (command.ParametersCount <= 0)
            {
                var pages = new PageCollection();
                EmbedBuilder embed = null;
                foreach (var module in ModuleCollection.Modules)
                {
                    if (embed == null || embed.Fields.Count % 3 == 0)
                    {
                        embed = new EmbedBuilder()
                            .WithTitle("List of modules")
                            .WithFooter($"Type `{config.CommandPrefix}help CommandName` to see usage and help for a specific command.");

                        pages.Add(embed);
                    }

                    var description = module.Description + "\n";
                    foreach (var handledCommand in module.HandledCommands)
                        description += "\n**" + config.CommandPrefix + handledCommand.InvokeString + "** – " + handledCommand.Description + "";
                    
                    embed.AddField(x => x.WithName(":pushpin: " + module.Name).WithValue(description));
                }
                
                await command.Reply(Communicator, pages, true).ConfigureAwait(false);
            }
            else
            {
                //Try to find the command
                CommandRegistration commandRegistration = null;
                string searchedCommand = command.Body.StartsWith(config.CommandPrefix) ? command.Body.Substring(config.CommandPrefix.Length) : command.Body;
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
                    .WithTitle($"Command {config.CommandPrefix}{commandRegistration.InvokeString}")
                    .WithDescription(commandRegistration.Description);

                if (!string.IsNullOrEmpty(commandRegistration.GetUsage(config.CommandPrefix)))
                    embed.AddField(x => x.WithName("Usage").WithValue(commandRegistration.GetUsage(config.CommandPrefix)));

                await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
            }
        }

        [Command("about", "Bot and version information.")]
        public async Task About(ICommand command)
        {
            var guilds = await Client.GetGuildsAsync().ConfigureAwait(false);
            var config = await Settings.ReadGlobal<BotConfig>();

            var embed = new EmbedBuilder()
                .WithTitle($"{Client.CurrentUser.Username} (DustyBot v{typeof(Bot).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version})")
                .AddInlineField("Author", "Yebafan#3517")
                .AddInlineField("Owners", string.Join("\n", config.OwnerIDs))
                .AddInlineField("Presence", $"{guilds.Count} servers")
                .AddInlineField("Framework", "v" + typeof(Framework.Framework).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version)
                .AddInlineField("Web", "https://github.com/yebafan/DustyBot")
                .WithThumbnailUrl(Client.CurrentUser.GetAvatarUrl());

            await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }

        [Command("listServers", "List all servers the bot is on.")]
        public async Task ListServers(ICommand command)
        {
            var pages = new PageCollection();
            foreach (var guild in await Client.GetGuildsAsync().ConfigureAwait(false))
            {
                if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 10 == 0)
                    pages.Add(new EmbedBuilder());
                
                var owner = await guild.GetOwnerAsync();
                
                pages.Last.Embed.AddField(x => x
                    .WithName(guild.Name)
                    .WithValue($"{guild.Id}\n{((SocketGuild)guild).MemberCount} members\nOwned by {owner.Username}#{owner.Discriminator} ({owner.Id})"));
            }

            await command.Reply(Communicator, pages, true).ConfigureAwait(false);
        }
    }
}
