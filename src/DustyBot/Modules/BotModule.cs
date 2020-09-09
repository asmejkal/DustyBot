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
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using DustyBot.Helpers;
using System.Reflection;
using Discord.WebSocket;
using System.Net;
using System.IO;
using Discord.Net;
using DustyBot.Definitions;

namespace DustyBot.Modules
{
    [Module("Bot", "Help and bot-related commands.")]
    class BotModule : Framework.Modules.Module
    {
        public ICommunicator Communicator { get; }
        public ISettingsProvider Settings { get; }
        public IModuleCollection ModuleCollection { get; }
        public DiscordSocketClient Client { get; }

        public BotModule(ICommunicator communicator, ISettingsProvider settings, IModuleCollection moduleCollection, DiscordSocketClient client)
        {
            Communicator = communicator;
            Settings = settings;
            ModuleCollection = moduleCollection;
            Client = client;
        }

        [Command("help", "Prints usage info.", CommandFlags.DirectMessageAllow)]
        [Parameter("Command", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "show usage of a specific command")]
        [Example("event add")]
        public async Task Help(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();
            if (command.ParametersCount <= 0)
            {
                await command.Message.Channel.SendMessageAsync(string.Empty, embed: (await HelpBuilder.GetHelpEmbed(Settings)).Build()).ConfigureAwait(false);
            }
            else
            {
                //Try to find the command
                var invoker = new string(command["Command"].AsString.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
                if (invoker.StartsWith(config.CommandPrefix))
                    invoker = invoker.Substring(config.CommandPrefix.Length);

                var verbs = command["Command"].AsString.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x => x.ToLowerInvariant()).ToList();
                var findResult = TryFindRegistration(invoker, verbs);
                if (findResult == null)
                    throw new Framework.Exceptions.IncorrectParametersCommandException("This is not a recognized command.");

                var (commandRegistration, _) = findResult.Value;

                //Build response
                var embed = new EmbedBuilder()
                    .WithTitle($"Command {commandRegistration.PrimaryUsage.InvokeUsage}")
                    .WithDescription(commandRegistration.Description)
                    .WithFooter("If a parameter contains spaces, add quotes: \"he llo\". Parameters marked \"...\" need no quotes.");

                embed.AddField(x => x.WithName("Usage").WithValue(DefaultCommunicator.BuildUsageString(commandRegistration, config)));

                await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
            }
        }

        [Command("supporters", "See the people who help keep this bot up and running.", CommandFlags.DirectMessageAllow)]
        public async Task Supporters(ICommand command)
        {
            var settings = await Settings.ReadGlobal<SupporterSettings>();
            var result = new StringBuilder();
            result.AppendLine($":revolving_hearts: People who keep the bot up and running :revolving_hearts:");
            foreach (var supporter in settings.Supporters)
                result.AppendLine($"{supporter.Name}");

            result.AppendLine();
            result.AppendLine($"Support the bot at <{IntegrationConstants.PatreonPage}>");

            await command.Reply(Communicator, result.ToString());
        }

        [Command("about", "Shows information about the bot.", CommandFlags.DirectMessageAllow)]
        public async Task About(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();

            var users = new HashSet<ulong>();
            foreach (var guild in Client.Guilds)
                users.UnionWith(guild.Users.Select(x => x.Id));

            var uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime());

            var embed = new EmbedBuilder()
                .WithTitle($"{Client.CurrentUser.Username} (DustyBot v{typeof(Bot).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version})")
                .AddField("Author", "Yeba#3517", true)
                .AddField("Owners", string.Join("\n", config.OwnerIDs), true)
                .AddField("Presence", $"{users.Count} users\n{Client.Guilds.Count} servers", true)
                .AddField("Framework", "v" + typeof(Framework.Framework).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, true)
                .AddField("Uptime", $"{(uptime.Days > 0 ? $"{uptime.Days}d " : "") + (uptime.Hours > 0 ? $"{uptime.Hours}h " : "") + $"{uptime.Minutes}min "}", true)
                .AddField("Web", WebConstants.WebsiteRoot, true)
                .WithThumbnailUrl(Client.CurrentUser.GetAvatarUrl());

            await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }

        [Command("feedback", "Suggest a modification or report an issue.", CommandFlags.DirectMessageAllow)]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder)]
        public async Task Feedback(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();

            foreach (var owner in config.OwnerIDs)
            {
                var user = (IUser)Client.GetUser(owner);
                if (user == null)
                    user = await Client.Rest.GetUserAsync(owner);

                var author = command.Message.Author;
                await user.SendMessageAsync($"Suggestion from **{author.Username}#{author.Discriminator}** ({author.Id}) on **{command.Guild.Name}**:\n\n" + command["Message"]);
            }

            await command.ReplySuccess(Communicator, "Thank you for your feedback!").ConfigureAwait(false);
        }

        [Command("invite", "Shows a link to invite the bot to your server.", CommandFlags.DirectMessageAllow)]
        public async Task Invite(ICommand command)
        {
            await command.Reply(Communicator, $"<https://discordapp.com/oauth2/authorize?client_id={Client.CurrentUser.Id}&scope=bot&permissions=0>");
        }

        [Command("servers", "Lists all servers the bot is on.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        public async Task ListServers(ICommand command)
        {
            var pages = new PageCollection();
            foreach (var guild in Client.Guilds.OrderByDescending(x => x.MemberCount))
            {
                if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 10 == 0)
                    pages.Add(new EmbedBuilder());

                pages.Last.Embed.AddField(x => x
                    .WithName(guild?.Name ?? "Unknown")
                    .WithValue($"{guild?.Id}\n{guild?.MemberCount} members\nOwned by {guild?.Owner?.Username}#{guild?.Owner?.Discriminator} ({guild?.OwnerId})"));
            }

            await command.Reply(Communicator, pages, true).ConfigureAwait(false);
        }

        [Command("server", "global", "Shows information about a server.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("ServerNameOrId", ParameterType.String, ParameterFlags.Remainder, "Id or name of a server")]
        public async Task ServerInfo(ICommand command)
        {
            SocketGuild guild;
            if (command["ServerNameOrId"].AsId.HasValue)
                guild = Client.GetGuild(command["ServerNameOrId"].AsId.Value);
            else
                guild = Client.Guilds.FirstOrDefault(x => x.Name == command["ServerNameOrId"]) as SocketGuild;

            if (guild == null)
            {
                await command.ReplyError(Communicator, "No guild with this name or ID.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(guild.Name)
                .WithThumbnailUrl(guild.IconUrl)
                .AddField(x => x.WithIsInline(true).WithName("ID").WithValue(guild.Id))
                .AddField(x => x.WithIsInline(true).WithName("Owner").WithValue($"{guild.Owner.Username}#{guild.Owner.Discriminator}"))
                .AddField(x => x.WithIsInline(true).WithName("Owner ID").WithValue(guild.OwnerId))
                .AddField(x => x.WithIsInline(true).WithName("Members").WithValue(guild.MemberCount))
                .AddField(x => x.WithIsInline(true).WithName("Created").WithValue(guild.CreatedAt.ToString("dd.MM.yyyy H:mm:ss UTC")));

            await command.Message.Channel.SendMessageAsync(string.Empty, embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("setavatar", "Changes the bot's avatar.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("Url", ParameterType.String, ParameterFlags.Optional)]
        [Comment("Attach your new image to the message or provide a link.")]
        public async Task SetAvatar(ICommand command)
        {
            if (command.ParametersCount <= 0 && command.Message.Attachments.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Missing attachment.");

            var request = WebRequest.CreateHttp(command["Url"].HasValue ? command["Url"] : command.Message.Attachments.First().Url);
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

        [Command("setname", "Changes the bot's username.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("NewName", ParameterType.String, ParameterFlags.Remainder)]
        public async Task SetName(ICommand command)
        {
            await Client.CurrentUser.ModifyAsync(x => x.Username = (string)command["NewName"]);
            await command.ReplySuccess(Communicator, "Username was changed!").ConfigureAwait(false);
        }

        [Command("help", "dump", "Generates a list of all commands.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        public async Task DumpHelp(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();
            var result = new StringBuilder();
            var preface = new StringBuilder("<div class=\"row\"><div class=\"col-lg-12 section-heading\" style=\"margin-bottom: 0px\">\n<h3><img class=\"feature-icon-big\" src=\"img/compass.png\"/>Quick navigation</h3>\n");
            int counter = 0;
            foreach (var module in ModuleCollection.Modules.Where(x => !x.Hidden))
            {
                var anchor = WebConstants.GetModuleWebAnchor(module.Name);
                preface.AppendLine($"<p class=\"text-muted\"><a href=\"#{anchor}\"><img class=\"feature-icon-small\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</a> – {module.Description}</p>");

                result.AppendLine($"<div class=\"row\"><div class=\"col-lg-12\"><a class=\"anchor\" id=\"{anchor}\"></a><h3><img class=\"feature-icon-big\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</h3>");
                result.AppendLine($"<p class=\"text-muted\">{module.Description}</p>");

                foreach (var handledCommand in module.HandledCommands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden) && !x.Flags.HasFlag(CommandFlags.OwnerOnly)))
                {
                    counter++;

                    if (handledCommand.PrimaryUsage.InvokeUsage == "calendar create")
                        result.AppendLine("</br><p class=\"text-muted\">Calendars are automatically updated messages which display parts of the schedule.</p>");
                    else if (handledCommand.PrimaryUsage.InvokeUsage == "roles group add")
                        result.AppendLine("</br><p class=\"text-muted\">Use groups to limit the number of roles a user may have.</p>");

                    result.AppendLine($"<p data-target=\"#usage{counter}\" data-toggle=\"collapse\" class=\"paramlistitem\">" +
                        $"<i class=\"fa fa-angle-right\" style=\"margin-right: 3px;\"></i><span class=\"paramlistcode\">{handledCommand.PrimaryUsage.InvokeUsage}</span> – {handledCommand.Description} " +
                        //string.Join(" ", handledCommand.RequiredPermissions.Select(x => $"<span class=\"perm\">{x.ToString().SplitCamelCase()}</span>")) +
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

            preface.AppendLine("</div></div>");
            preface.AppendLine("<hr/>");
            preface.Append(result);
            using (var stream = new MemoryStream(Encoding.Unicode.GetBytes(preface.ToString())))
            {
                await command.Message.Channel.SendFileAsync(stream, "output.html");
            }
        }

        [Command("cleanup", "commands", "Cleans output of the bot's commands.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("Count", ParameterType.Int, ParameterFlags.Remainder, "number of commands to cleanup")]
        public async Task CleanupCommands(ICommand command)
        {
            var messages = await command.Message.Channel.GetMessagesAsync(500).ToListAsync();
            var tasks = new List<Task>();
            var count = 0;
            foreach (var m in messages.SelectMany(x => x).Where(x => x.Author.Id == Client.CurrentUser.Id))
            {
                if (++count > command["Count"].AsInt)
                    break;

                tasks.Add(m.DeleteAsync());
            }

            await Task.WhenAll(tasks);
        }

        [Command("server", "settings", "get", "Gets settings for a server.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("ServerId", ParameterType.Id, ParameterFlags.Optional)]
        [Parameter("Module", ParameterType.String, "LiteDB collection name")]
        [Parameter("Raw", @"^raw$", ParameterType.String, ParameterFlags.Optional, "get unformatted output")]
        public async Task GetSettings(ICommand command)
        {
            var serverId = command[0].AsId ?? command.GuildId;
            var result = await Settings.DumpSettings(serverId, command["Module"], command["Raw"].HasValue);
            
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(result);
                await writer.FlushAsync();
                stream.Position = 0;

                await command.Channel.SendFileAsync(stream, $"{command["Module"]}-{serverId}.json");
            }
        }

        [Command("server", "settings", "set", "Sets settings for a server.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("Module", ParameterType.String, "LiteDB collection name")]
        public async Task SetSettings(ICommand command)
        {
            if (command.Message.Attachments.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Missing attachment.");

            var request = WebRequest.CreateHttp(command.Message.Attachments.First().Url);
            using (var response = await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var json = await reader.ReadToEndAsync();
                await Settings.SetSettings(command["Module"], json);
            }

            await command.ReplySuccess(Communicator, "Done!").ConfigureAwait(false);
        }

        [Command("supporters", "add", "Adds a supporter.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Alias("supporter", "add")]
        [Parameter("Position", ParameterType.UInt, ParameterFlags.Optional)]
        [Parameter("Name", ParameterType.String, ParameterFlags.Remainder)]
        public async Task AddSupporter(ICommand command)
        {
            await Settings.ModifyGlobal<SupporterSettings>(x => 
                x.Supporters.Insert(command["Position"].AsInt ?? x.Supporters.Count, new Supporter() { Name = command["Name"] }));

            await command.ReplySuccess(Communicator, "Done!");
        }

        [Command("supporters", "remove", "Removes a supporter.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Alias("supporter", "remove")]
        [Parameter("Name", ParameterType.String, ParameterFlags.Remainder)]
        public async Task RemoveSupporter(ICommand command)
        {
            int removed = 0;
            await Settings.ModifyGlobal<SupporterSettings>(x => removed = x.Supporters.RemoveAll(y => y.Name == command["Name"]));

            await command.ReplySuccess(Communicator, $"Removed {removed} supporters.");
        }

        static string Markdown(string input)
        {
            bool inside = false;
            input = input.Split('`').Aggregate((x, y) => x + ((inside = !inside) ? "<span class=\"param\">" : "</span>") + y);

            inside = false;
            input = input.Split(new string[] { "**" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<b>" : "</b>") + y);

            inside = false;
            input = input.Split(new string[] { "*" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<i>" : "</i>") + y);

            inside = false;
            input = input.Split(new string[] { "__" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<u>" : "</u>") + y);
            
            input = input.Replace("\r\n", "<br/>");
            input = input.Replace("\n", "<br/>");

            return input;
        }

        static string BuildWebUsageString(CommandRegistration commandRegistration, Framework.Config.IEssentialConfig config)
        {
            string usage = $"{config.CommandPrefix}{commandRegistration.PrimaryUsage.InvokeUsage}";
            foreach (var param in commandRegistration.Parameters.Where(x => !x.Flags.HasFlag(ParameterFlags.Hidden)))
            {
                string tmp = param.Name;
                if (param.Flags.HasFlag(ParameterFlags.Remainder))
                    tmp += "...";

                if (param.Flags.HasFlag(ParameterFlags.Optional))
                    tmp = $"[{tmp}]";

                usage += $" `{tmp}`";
            }

            var paramDescriptions = new StringBuilder();
            foreach (var param in commandRegistration.Parameters.Where(x => !x.Flags.HasFlag(ParameterFlags.Hidden) && !string.IsNullOrWhiteSpace(x.GetDescription(config.CommandPrefix))))
            {
                string tmp = $"● `{param.Name}` ‒ ";
                if (param.Flags.HasFlag(ParameterFlags.Optional))
                    tmp += "optional; ";

                tmp += param.GetDescription(config.CommandPrefix);
                paramDescriptions.Append(paramDescriptions.Length <= 0 ? tmp : "<br/>" + tmp);
            }

            var examples = commandRegistration.Examples
                .Select(x => $"{config.CommandPrefix}{commandRegistration.PrimaryUsage.InvokeUsage} {x}")
                .DefaultIfEmpty()
                .Aggregate((x, y) => x + "<br/>" + y);

            var result = new StringBuilder($"<span class=\"usagecode\">{Markdown(usage)}</span>");
            if (paramDescriptions.Length > 0)
                result.Append("<br/><br/>" + Markdown(paramDescriptions.ToString()));

            if (!string.IsNullOrWhiteSpace(commandRegistration.GetComment(config.CommandPrefix)))
                result.Append("<br/><br/>" + Markdown(commandRegistration.GetComment(config.CommandPrefix)));

            if (!string.IsNullOrWhiteSpace(examples))
                result.Append("<br/><br/><u>Examples:</u><br/><code>" + Markdown(examples) + "</code>");

            if (commandRegistration.Aliases.Any(x => !x.Hidden))
            {
                result.Append("<br/><span class=\"aliases\">Also as " 
                    + commandRegistration.Aliases.Where(x => !x.Hidden).Select(x => $"<span class=\"alias\">{x.InvokeUsage}</span>").WordJoin(lastSeparator: " or ") 
                    + "</span>");
            }                

            return result.ToString();
        }

        (CommandRegistration registration, CommandRegistration.Usage usage)? TryFindRegistration(string invoker, ICollection<string> verbs)
        {
            foreach (var module in ModuleCollection.Modules.Where(x => !x.Hidden))
            {
                foreach (var handledCommand in module.HandledCommands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden)))
                {
                    foreach (var usage in handledCommand.EveryUsage)
                    {
                        if (string.Compare(usage.InvokeString, invoker, true) == 0 && verbs.SequenceEqual(usage.Verbs.Select(x => x.ToLowerInvariant())))
                        {
                            return (handledCommand, usage);
                        }
                    }
                }
            }

            return null;
        }
    }
}
