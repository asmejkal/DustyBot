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
using DustyBot.Helpers;
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
        [Parameter("Command", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "show usage of a specific command")]
        [Comment("Use without parameters to see a list of modules and commands.")]
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
                            .WithDescription("Also on [web](http://dustybot.info/reference). Join the [support server](https://discord.gg/mKKJFvZ) if you need help with any of the commands.")
                            .WithTitle("Commands")
                            .WithFooter($"Type '{config.CommandPrefix}help command' to see usage of a specific command."));
                    }

                    var description = module.Description + "\n";
                    foreach (var handledCommand in module.HandledCommands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden) && !x.Flags.HasFlag(CommandFlags.OwnerOnly)))
                        description += $"\n`{config.CommandPrefix}{handledCommand.InvokeUsage}` – {handledCommand.Description}";
                    
                    pages.Last.Embed.AddField(x => x.WithName(":pushpin: " + module.Name).WithValue(description));
                }
                
                await command.Reply(Communicator, pages).ConfigureAwait(false);
            }
            else
            {
                //Try to find the command
                CommandRegistration commandRegistration = null;
                var invoker = new string(command.Body.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
                if (invoker.StartsWith(config.CommandPrefix))
                    invoker = invoker.Substring(config.CommandPrefix.Length);

                var verbs = command.Body.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x => x.ToLowerInvariant()).ToList();
                foreach (var module in ModuleCollection.Modules.Where(x => !x.Hidden))
                {
                    foreach (var handledCommand in module.HandledCommands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden)))
                    {
                        if (string.Compare(handledCommand.InvokeString, invoker, true) == 0 && verbs.SequenceEqual(handledCommand.Verbs.Select(x => x.ToLowerInvariant())))
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
                    .WithTitle($"Command {commandRegistration.InvokeUsage}")
                    .WithDescription(commandRegistration.Description)
                    .WithFooter("If a parameter contains spaces, add quotes: \"he llo\". Parameters marked \"...\" need no quotes.");
                
                embed.AddField(x => x.WithName("Usage").WithValue(DefaultCommunicator.BuildUsageString(commandRegistration, config)));

                await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
            }
        }

        [Command("about", "Bot and version information.", CommandFlags.RunAsync)]
        public async Task About(ICommand command)
        {
            var guilds = await Client.GetGuildsAsync().ConfigureAwait(false);
            var config = await Settings.ReadGlobal<BotConfig>();

            var users = new HashSet<ulong>();
            foreach (var guild in guilds)
            {
                foreach (var user in await guild.GetUsersAsync().ConfigureAwait(false))
                    users.Add(user.Id);
            }

            var embed = new EmbedBuilder()
                .WithTitle($"{Client.CurrentUser.Username} (DustyBot v{typeof(Bot).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version})")
                .AddInlineField("Author", "Yebafan#3517")
                .AddInlineField("Owners", string.Join("\n", config.OwnerIDs))
                .AddInlineField("Presence", $"{users.Count} users\n{guilds.Count} servers")
                .AddInlineField("Framework", "v" + typeof(Framework.Framework).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version)
                .AddInlineField("Web", "http://dustybot.info")
                .WithThumbnailUrl(Client.CurrentUser.GetAvatarUrl());

            await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }

        [Command("servers", "Lists all servers the bot is on.")]
        public async Task ListServers(ICommand command)
        {
            var pages = new PageCollection();
            foreach (var guild in (await Client.GetGuildsAsync().ConfigureAwait(false)).Select(x => x as SocketGuild).OrderByDescending(x => x.MemberCount))
            {
                if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 10 == 0)
                    pages.Add(new EmbedBuilder());
                                
                pages.Last.Embed.AddField(x => x
                    .WithName(guild.Name)
                    .WithValue($"{guild.Id}\n{guild.MemberCount} members\nOwned by {guild.Owner.Username}#{guild.Owner.Discriminator} ({guild.OwnerId})"));
            }

            await command.Reply(Communicator, pages, true).ConfigureAwait(false);
        }

        [Command("feedback", "Suggest a modification or report an issue.")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder)]
        public async Task Feedback(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();

            foreach (var owner in config.OwnerIDs)
            {
                var user = await Client.GetUserAsync(owner);
                var author = command.Message.Author;
                await user.SendMessageAsync($"Suggestion from **{author.Username}#{author.Discriminator}** ({author.Id}) on **{command.Guild.Name}**:\n\n" + command["Message"]);
            }

            await command.ReplySuccess(Communicator, "Thank you for your feedback!").ConfigureAwait(false);
        }

        [Command("setavatar", "Changes the bot's avatar.", CommandFlags.RunAsync | CommandFlags.OwnerOnly)]
        [Parameter("Url", ParameterType.String, ParameterFlags.Optional)]
        [Comment("Attach your new image to the message or provide a link.")]
        public async Task SetAvatar(ICommand command)
        {
            if (command.ParametersCount <= 0 && command.Message.Attachments.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Missing attachment.");

            var request = WebRequest.CreateHttp((string)command[0] ?? command.Message.Attachments.First().Url);
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

        [Command("setname", "Changes the bot's username.", CommandFlags.RunAsync | CommandFlags.OwnerOnly)]
        [Parameter("NewName", ParameterType.String, ParameterFlags.Remainder)]
        public async Task SetName(ICommand command)
        {
            await Client.CurrentUser.ModifyAsync(x => x.Username = (string)command["NewName"]);
            await command.ReplySuccess(Communicator, "Username was changed!").ConfigureAwait(false);
        }

        [Command("dump", "commands", "Generates a list of all commands.", CommandFlags.RunAsync | CommandFlags.OwnerOnly)]
        public async Task Commandlist(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();
            var result = new StringBuilder();
            int counter = 0;
            foreach (var module in ModuleCollection.Modules.Where(x => !x.Hidden))
            {
                result.AppendLine($"<div class=\"row\"><div class=\"col-lg-12\"><h3>{module.Name}</h3>");
                result.AppendLine($"<p class=\"text-muted\">{module.Description}</p>");

                foreach (var handledCommand in module.HandledCommands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden) && !x.Flags.HasFlag(CommandFlags.OwnerOnly)))
                {
                    counter++;
                    result.AppendLine($"<p data-target=\"#usage{counter}\" data-toggle=\"collapse\" class=\"paramlistitem\">" +
                        $"&#9662; <span class=\"paramlistcode\">{handledCommand.InvokeUsage}</span> – {handledCommand.Description} " +
                        string.Join(" ", handledCommand.RequiredPermissions.Select(x => $"<span class=\"perm\">{x.ToString().SplitCamelCase()}</span>")) +
                        "</p>");

                    var usage = BuildWebUsageString(handledCommand, config);
                    if (string.IsNullOrEmpty(usage))
                        continue;

                    result.AppendLine($"<div id=\"usage{counter}\" class=\"collapse usage\">");
                    result.Append(usage);
                    result.AppendLine("</div>");
                }

                result.AppendLine("<br/></div></div>");
            }

            using (var stream = new MemoryStream(Encoding.Unicode.GetBytes(result.ToString())))
            {
                await command.Message.Channel.SendFileAsync(stream, "output.html");
            }
        }

        static string Markdown(string input)
        {
            bool inside = false;
            input = input.Split('`').Aggregate((x, y) => x + ((inside = !inside) ? "<span class=\"param\">" : "</span>") + y);
            input = input.Split(new string[] { "**" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<b>" : "</b>") + y);
            input = input.Split(new string[] { "__" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<u>" : "</u>") + y);
            input = input.Replace("\r\n", "<br/>");
            input = input.Replace("\n", "<br/>");

            return input;
        }

        static string BuildWebUsageString(CommandRegistration commandRegistration, Framework.Config.IEssentialConfig config)
        {
            string usage = $"{config.CommandPrefix}{commandRegistration.InvokeUsage}";
            foreach (var param in commandRegistration.Parameters)
            {
                string tmp = param.Name;
                if (param.Flags.HasFlag(ParameterFlags.Remainder))
                    tmp += "...";

                if (param.Flags.HasFlag(ParameterFlags.Optional))
                    tmp = $"[{tmp}]";

                usage += $" `{tmp}`";
            }

            string paramDescriptions = string.Empty;
            foreach (var param in commandRegistration.Parameters.Where(x => !string.IsNullOrWhiteSpace(x.GetDescription(config.CommandPrefix))))
            {
                string tmp = $"● `{param.Name}` ‒ ";
                if (param.Flags.HasFlag(ParameterFlags.Optional) || param.Flags.HasFlag(ParameterFlags.Remainder))
                {
                    if (param.Flags.HasFlag(ParameterFlags.Optional))
                        tmp += "optional";

                    if (param.Flags.HasFlag(ParameterFlags.Remainder))
                        tmp += param.Flags.HasFlag(ParameterFlags.Optional) ? " remainder" : "remainder";

                    tmp += "; ";
                }

                tmp += param.GetDescription(config.CommandPrefix);
                paramDescriptions += string.IsNullOrEmpty(paramDescriptions) ? tmp : "<br/>" + tmp;
            }

            var examples = commandRegistration.Examples
                .Select(x => $"{config.CommandPrefix}{commandRegistration.InvokeUsage} {x}")
                .DefaultIfEmpty()
                .Aggregate((x, y) => x + "<br/>" + y);

            return $"<span class=\"usagecode\">{Markdown(usage)}</span>" +
                (string.IsNullOrWhiteSpace(paramDescriptions) ? string.Empty : "<br/><br/>" + Markdown(paramDescriptions)) +
                (string.IsNullOrWhiteSpace(commandRegistration.GetComment(config.CommandPrefix)) ? string.Empty : "<br/><br/>" + Markdown(commandRegistration.GetComment(config.CommandPrefix))) +
                (string.IsNullOrWhiteSpace(examples) ? string.Empty : "<br/><br/><u>Examples:</u><br/><code>" + Markdown(examples) + "</code>");
        }
    }
}
