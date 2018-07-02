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
using System.Net;
using System.IO;
using Discord.Net;

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
                foreach (var module in ModuleCollection.Modules.Where(x => !x.Hidden))
                {
                    if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 4 == 0)
                    {
                        pages.Add(new EmbedBuilder()
                            .WithTitle("List of modules")
                            .WithFooter($"Type `{config.CommandPrefix}help CommandName` to see usage and help for a specific command."));
                    }

                    var description = module.Description + "\n";
                    foreach (var handledCommand in module.HandledCommands.Where(x => !x.Hidden))
                        description += $"\n**{config.CommandPrefix}{handledCommand.InvokeString}{(!string.IsNullOrEmpty(handledCommand.Verb) ? $" {handledCommand.Verb}" : "")}** – {handledCommand.Description}";
                    
                    pages.Last.Embed.AddField(x => x.WithName(":pushpin: " + module.Name).WithValue(description));
                }
                
                await command.Reply(Communicator, pages, true).ConfigureAwait(false);
            }
            else
            {
                //Try to find the command
                CommandRegistration commandRegistration = null;
                var invoker = new string(command.Body.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
                if (invoker.StartsWith(config.CommandPrefix))
                    invoker = invoker.Substring(config.CommandPrefix.Length);

                var verb = new string(command.Body.Skip(config.CommandPrefix.Length + invoker.Length).SkipWhile(c => char.IsWhiteSpace(c)).TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
                foreach (var module in ModuleCollection.Modules.Where(x => !x.Hidden))
                {
                    foreach (var handledCommand in module.HandledCommands.Where(x => !x.Hidden))
                    {
                        if (string.Compare(handledCommand.InvokeString, invoker, true) == 0 && string.Compare(verb, handledCommand.Verb ?? string.Empty, true) == 0)
                        {
                            commandRegistration = handledCommand;
                            break;
                        }
                    }

                    if (commandRegistration != null)
                        break;
                }

                if (commandRegistration == null)
                    throw new Framework.Exceptions.IncorrectParametersCommandException("This is not a recognized command.");

                //Build response
                var embed = new EmbedBuilder()
                    .WithTitle($"Command {config.CommandPrefix}{commandRegistration.InvokeString} {commandRegistration.Verb ?? ""}")
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

        [Command("listservers", "List all servers the bot is on.")]
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

        [Command("setavatar", "Changes the bot's avatar. Bot owner only."), RunAsync]
        [OwnerOnly, Hidden]
        [Usage("{p}setavatar [AttachmentUrl]\n\nAttach your new image to the message or provide a link.")]
        public async Task SetAvatar(ICommand command)
        {
            if (command.ParametersCount <= 0 && command.Message.Attachments.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Missing attachment.");

            var request = WebRequest.CreateHttp((string)command.GetParameter(0) ?? command.Message.Attachments.First().Url);
            using (var response = await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var memStream = new MemoryStream())
            {
                await stream.CopyToAsync(memStream);
                memStream.Position = 0;

                var image = new Image(memStream);

                try
                {
                    await Client.CurrentUser.ModifyAsync(x => x.Avatar = image);
                }
                catch (RateLimitedException)
                {
                    await command.ReplyError(Communicator, "You are changing avatars too fast, wait a few minutes and try again.");
                    return;
                }
            }

            await command.ReplySuccess(Communicator, "Avatar was changed!").ConfigureAwait(false);
        }

        [Command("setname", "Changes the bot's username. Bot owner only."), RunAsync]
        [OwnerOnly, Hidden]
        [Parameters(ParameterType.String)]
        [Usage("{p}setname NewName")]
        public async Task SetName(ICommand command)
        {
            await Client.CurrentUser.ModifyAsync(x => x.Username = command.Body);
            await command.ReplySuccess(Communicator, "Username was changed!").ConfigureAwait(false);
        }
    }
}
