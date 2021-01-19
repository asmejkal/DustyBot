using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Settings;
using DustyBot.Helpers;
using System.Reflection;
using Discord.WebSocket;
using System.Net;
using System.IO;
using Discord.Net;
using DustyBot.Core.Formatting;
using DustyBot.Definitions;
using DustyBot.Database.Services;
using DustyBot.Framework.Exceptions;
using DustyBot.Core.Async;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework;
using DustyBot.Framework.Reflection;
using Markdig;
using Microsoft.Extensions.Options;
using DustyBot.Configuration;

namespace DustyBot.Modules
{
    [Module("Bot", "Help and bot-related commands.")]
    internal sealed class BotModule : IDisposable
    {
        private readonly BaseSocketClient _client;
        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly WebsiteWalker _websiteWalker;
        private readonly IOptions<WebOptions> _webOptions;
        private readonly IOptions<BotOptions> _botOptions;
        private readonly HelpBuilder _helpBuilder;

        public BotModule(
            BaseSocketClient client, 
            ICommunicator communicator, 
            ISettingsService settings, 
            IFrameworkReflector frameworkReflector, 
            WebsiteWalker websiteWalker,
            IOptions<WebOptions> webOptions,
            IOptions<BotOptions> botOptions,
            HelpBuilder helpBuilder)
        {
            _client = client;
            _communicator = communicator;
            _settings = settings;
            _frameworkReflector = frameworkReflector;
            _websiteWalker = websiteWalker;
            _webOptions = webOptions;
            _botOptions = botOptions;
            _helpBuilder = helpBuilder;

            _client.MessageReceived += HandleMessageReceived;
        }

        [Command("help", "Prints usage info.", CommandFlags.DirectMessageAllow)]
        [Parameter("Command", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "show usage of a specific command")]
        [Example("event add")]
        public async Task Help(ICommand command)
        {
            if (command.ParametersCount <= 0)
            {
                await command.Reply(_helpBuilder.GetHelpEmbed(command.Prefix).Build());
            }
            else
            {
                //Try to find the command
                var invoker = new string(command["Command"].AsString.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
                if (invoker.StartsWith(command.Prefix))
                    invoker = invoker.Substring(command.Prefix.Length);
                else if (invoker.StartsWith(_botOptions.Value.DefaultCommandPrefix))
                    invoker = invoker.Substring(_botOptions.Value.DefaultCommandPrefix.Length);

                var verbs = command["Command"].AsString.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x => x.ToLowerInvariant()).ToList();
                var findResult = TryFindRegistration(invoker, verbs);
                if (findResult == null)
                    throw new IncorrectParametersCommandException("This is not a recognized command.");

                var (commandRegistration, _) = findResult.Value;

                //Build response
                var embed = new EmbedBuilder()
                    .WithTitle($"Command {commandRegistration.PrimaryUsage.InvokeUsage}")
                    .WithDescription(commandRegistration.Description)
                    .WithFooter("If a parameter contains spaces, add quotes: \"he llo\". Parameters marked \"...\" need no quotes.");

                embed.AddField(x => x.WithName("Usage").WithValue(_communicator.BuildCommandUsage(commandRegistration, command.Prefix)));

                await command.Reply(embed.Build());
            }
        }

        [Command("prefix", "Change the command prefix on your server.")]
        [Alias("prefix", "set")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Prefix", ParameterType.String, "the new command prefix (default is `>`)")]
        public async Task SetPrefix(ICommand command)
        {
            var prefix = command["Prefix"].AsString;
            if (prefix.Any(x => char.IsWhiteSpace(x)))
                throw new CommandException("The prefix must not contain spaces.");

            if (prefix.Length > 5)
                throw new CommandException("This prefix is too long.");

            await _settings.Modify(command.GuildId, (BotSettings x) => x.CommandPrefix = prefix);

            await command.Reply($"The command prefix is now `{prefix}` on this server!");
        }

        [Command("supporters", "See the people who help keep this bot up and running.", CommandFlags.DirectMessageAllow)]
        public async Task Supporters(ICommand command)
        {
            var settings = await _settings.ReadGlobal<SupporterSettings>();
            var result = new StringBuilder();
            result.AppendLine($":revolving_hearts: People who keep the bot up and running :revolving_hearts:");
            foreach (var supporter in settings.Supporters)
                result.AppendLine($"{supporter.Name}");

            result.AppendLine();
            result.AppendLine($"Support the bot at <{_webOptions.Value.PatreonUrl}>");

            await command.Reply(result.ToString());
        }

        [Command("about", "Shows information about the bot.", CommandFlags.DirectMessageAllow)]
        public async Task About(ICommand command)
        {
            var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();

            var embed = new EmbedBuilder()
                .WithTitle($"{_client.CurrentUser.Username} (DustyBot v{typeof(Bot).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version})")
                .AddField("Author", "Yeba#3517", true)
                .AddField("Owner", string.Join("\n", _botOptions.Value.OwnerID), true)
                .AddField("Presence", $"{_client.Guilds.Count} servers" + (_client is DiscordShardedClient sc ? $"\n{sc.Shards.Count} shards" : ""), true)
                .AddField("Framework", "v" + typeof(IFramework).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, true)
                .AddField("Uptime", $"{(uptime.Days > 0 ? $"{uptime.Days}d " : "") + (uptime.Hours > 0 ? $"{uptime.Hours}h " : "") + $"{uptime.Minutes}min "}", true)
                .AddField("Web", _websiteWalker.Root, true)
                .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());

            await command.Reply(embed.Build());
        }

        [Command("feedback", "Suggest a modification or report an issue.", CommandFlags.DirectMessageAllow)]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder)]
        public async Task Feedback(ICommand command)
        {
            var user = (IUser)_client.GetUser(_botOptions.Value.OwnerID) ?? await _client.Rest.GetUserAsync(_botOptions.Value.OwnerID);
            var author = command.Message.Author;
            await user.SendMessageAsync($"Suggestion from **{author.Username}#{author.Discriminator}** ({author.Id}) on **{command.Guild.Name}**:\n\n" + command["Message"]);

            await command.ReplySuccess("Thank you for your suggestion!");
        }

        [Command("invite", "Shows a link to invite the bot to your server.", CommandFlags.DirectMessageAllow)]
        public async Task Invite(ICommand command)
        {
            await command.Reply($"<https://discord.com/oauth2/authorize?client_id={_client.CurrentUser.Id}&scope=bot&permissions=8192>");
        }

        [Command("servers", "Lists all servers the bot is on.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        public async Task ListServers(ICommand command)
        {
            var pages = new PageCollection();
            foreach (var guild in _client.Guilds.OrderByDescending(x => x.MemberCount))
            {
                if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 10 == 0)
                    pages.Add(new EmbedBuilder());

                pages.Last.Embed.AddField(x => x
                    .WithName(guild?.Name ?? "Unknown")
                    .WithValue($"{guild?.Id}\n{guild?.MemberCount} members"));
            }

            await command.Reply(pages, true);
        }

        [Command("server", "global", "Shows information about a server.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("ServerNameOrId", ParameterType.String, ParameterFlags.Remainder, "Id or name of a server")]
        public async Task ServerInfo(ICommand command)
        {
            SocketGuild guild;
            if (command["ServerNameOrId"].AsId.HasValue)
                guild = _client.GetGuild(command["ServerNameOrId"].AsId.Value);
            else
                guild = _client.Guilds.FirstOrDefault(x => x.Name == command["ServerNameOrId"]) as SocketGuild;

            if (guild == null)
            {
                await command.ReplyError("No guild with this name or ID.");
                return;
            }

            var owner = (IGuildUser)guild.Owner ?? await _client.Rest.GetGuildUserAsync(guild.Id, guild.OwnerId);
            var embed = new EmbedBuilder()
                .WithTitle(guild.Name)
                .WithThumbnailUrl(guild.IconUrl)
                .AddField(x => x.WithIsInline(true).WithName("ID").WithValue(guild.Id))
                .AddField(x => x.WithIsInline(true).WithName("Owner").WithValue($"{owner.Username}#{owner.Discriminator}"))
                .AddField(x => x.WithIsInline(true).WithName("Owner ID").WithValue(guild.OwnerId))
                .AddField(x => x.WithIsInline(true).WithName("Members").WithValue(guild.MemberCount))
                .AddField(x => x.WithIsInline(true).WithName("Created").WithValue(guild.CreatedAt.ToString("dd.MM.yyyy H:mm:ss UTC")));

            await command.Reply(embed.Build());
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
                    await _client.CurrentUser.ModifyAsync(x => x.Avatar = image);
                }
                catch (RateLimitedException)
                {
                    await command.ReplyError("You are changing avatars too fast, wait a few minutes and try again.");
                    return;
                }
            }

            await command.ReplySuccess("Avatar was changed!");
        }

        [Command("setname", "Changes the bot's username.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        [Parameter("NewName", ParameterType.String, ParameterFlags.Remainder)]
        public async Task SetName(ICommand command)
        {
            await _client.CurrentUser.ModifyAsync(x => x.Username = (string)command["NewName"]);
            await command.ReplySuccess("Username was changed!");
        }

        [Command("help", "dump", "Generates a list of all commands.", CommandFlags.DirectMessageAllow | CommandFlags.OwnerOnly)]
        public async Task DumpHelp(ICommand command)
        {
            var result = new StringBuilder();
            var preface = new StringBuilder("<div class=\"row\"><div class=\"col-lg-12 section-heading\" style=\"margin-bottom: 0px\">\n<h3><img class=\"feature-icon-big\" src=\"img/compass.png\"/>Quick navigation</h3>\n");
            int counter = 0;
            foreach (var module in _frameworkReflector.Modules.Where(x => !x.Hidden))
            {
                var anchor = WebsiteWalker.GetModuleWebAnchor(module.Name);
                var description = ConvertToHtml(ReplacePlaceholders(module.Description));
                preface.AppendLine($"<p class=\"text-muted\"><a href=\"#{anchor}\"><img class=\"feature-icon-small\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</a> – {description}</p>");

                result.AppendLine($"<div class=\"row\"><div class=\"col-lg-12\"><a class=\"anchor\" id=\"{anchor}\"></a><h3><img class=\"feature-icon-big\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</h3>");
                result.AppendLine($"<p class=\"text-muted\">{description}</p>");

                foreach (var handledCommand in module.Commands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden) && !x.Flags.HasFlag(CommandFlags.OwnerOnly)))
                {
                    counter++;

                    if (handledCommand.PrimaryUsage.InvokeUsage == "calendar create")
                        result.AppendLine("</br><p class=\"text-muted\">Calendars are automatically updated messages which display parts of the schedule.</p>");
                    else if (handledCommand.PrimaryUsage.InvokeUsage == "roles group add")
                        result.AppendLine("</br><p class=\"text-muted\">Use groups to limit the number of roles a user may have.</p>");
                    else if (handledCommand.PrimaryUsage.InvokeUsage == "event add")
                        result.AppendLine("</br><p class=\"text-muted\">Events are the content of your schedule.</p>");

                    result.AppendLine($"<p data-target=\"#usage{counter}\" data-toggle=\"collapse\" class=\"paramlistitem\">" +
                        $"<i class=\"fa fa-angle-right\" style=\"margin-right: 3px;\"></i><span class=\"paramlistcode\">{handledCommand.PrimaryUsage.InvokeUsage}</span> – {handledCommand.Description} " +
                        //string.Join(" ", handledCommand.RequiredPermissions.Select(x => $"<span class=\"perm\">{x.ToString().SplitCamelCase()}</span>")) +
                        "</p>");

                    var usage = BuildWebUsageString(handledCommand, _botOptions.Value.DefaultCommandPrefix);
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
            foreach (var m in messages.SelectMany(x => x).Where(x => x.Author.Id == _client.CurrentUser.Id))
            {
                if (++count > command["Count"].AsInt)
                    break;

                tasks.Add(m.DeleteAsync());
            }

            await Task.WhenAll(tasks);
        }

        [Command("supporters", "add", "Adds a supporter.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Alias("supporter", "add")]
        [Parameter("Position", ParameterType.UInt, ParameterFlags.Optional)]
        [Parameter("Name", ParameterType.String, ParameterFlags.Remainder)]
        public async Task AddSupporter(ICommand command)
        {
            await _settings.ModifyGlobal<SupporterSettings>(x => 
                x.Supporters.Insert(command["Position"].AsInt ?? x.Supporters.Count, new Supporter() { Name = command["Name"] }));

            await command.ReplySuccess("Done!");
        }

        [Command("supporters", "remove", "Removes a supporter.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Alias("supporter", "remove")]
        [Parameter("Name", ParameterType.String, ParameterFlags.Remainder)]
        public async Task RemoveSupporter(ICommand command)
        {
            int removed = 0;
            await _settings.ModifyGlobal<SupporterSettings>(x => removed = x.Supporters.RemoveAll(y => y.Name == command["Name"]));

            await command.ReplySuccess($"Removed {removed} supporters.");
        }

        private string ReplacePlaceholders(string input)
        {
            return input.Replace(HelpPlaceholders.RolesGuideLink, _websiteWalker.RolesGuideUrl)
                .Replace(HelpPlaceholders.ScheduleGuideLink, _websiteWalker.ScheduleGuideUrl);
        }

        private static string ConvertToHtml(string input)
        {
            return Markdown.ToHtml(input)
                .Replace("<code>", "<span class=\"param\">")
                .Replace("</code>", "</span>")
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>");
        }

        private static string BuildWebUsageString(CommandInfo commandRegistration, string commandPrefix)
        {
            string usage = $"{commandPrefix}{commandRegistration.PrimaryUsage.InvokeUsage}";
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
            foreach (var param in commandRegistration.Parameters.Where(x => !x.Flags.HasFlag(ParameterFlags.Hidden) && !string.IsNullOrWhiteSpace(x.GetDescription(commandPrefix))))
            {
                string tmp = $"● `{param.Name}` ‒ ";
                if (param.Flags.HasFlag(ParameterFlags.Optional))
                    tmp += "optional; ";

                tmp += param.GetDescription(commandPrefix);
                paramDescriptions.Append(paramDescriptions.Length <= 0 ? tmp : "<br/>" + tmp);
            }

            var examples = commandRegistration.Examples
                .Select(x => $"{commandPrefix}{commandRegistration.PrimaryUsage.InvokeUsage} {x}")
                .DefaultIfEmpty()
                .Aggregate((x, y) => x + "<br/>" + y);

            var result = new StringBuilder($"<span class=\"usagecode\">{ConvertToHtml(usage)}</span>");
            if (paramDescriptions.Length > 0)
                result.Append("<br/><br/>" + ConvertToHtml(paramDescriptions.ToString()));

            if (!string.IsNullOrWhiteSpace(commandRegistration.GetComment(commandPrefix)))
                result.Append("<br/><br/>" + ConvertToHtml(commandRegistration.GetComment(commandPrefix)));

            if (!string.IsNullOrWhiteSpace(examples))
                result.Append("<br/><br/><u>Examples:</u><br/><code>" + ConvertToHtml(examples) + "</code>");

            if (commandRegistration.Aliases.Any(x => !x.Hidden))
            {
                result.Append("<br/><span class=\"aliases\">Also as " 
                    + commandRegistration.Aliases.Where(x => !x.Hidden).Select(x => $"<span class=\"alias\">{x.InvokeUsage}</span>").WordJoin(lastSeparator: " or ") 
                    + "</span>");
            }                

            return result.ToString();
        }

        private (CommandInfo registration, CommandInfo.Usage usage)? TryFindRegistration(string invoker, ICollection<string> verbs)
        {
            foreach (var module in _frameworkReflector.Modules.Where(x => !x.Hidden))
            {
                foreach (var handledCommand in module.Commands.Where(x => !x.Flags.HasFlag(CommandFlags.Hidden)))
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

        private Task HandleMessageReceived(SocketMessage message)
        {
            if (message.Content == $"<@{_client.CurrentUser.Id}>" || message.Content == $"<@!{_client.CurrentUser.Id}>")
            {
                TaskHelper.FireForget(async () =>
                {
                    var prefix = _botOptions.Value.DefaultCommandPrefix;
                    if (message.Channel is SocketTextChannel guildChannel)
                    {
                        var guildConfig = await _settings.Read<BotSettings>(guildChannel.Guild.Id);
                        if (!string.IsNullOrEmpty(guildConfig?.CommandPrefix))
                            prefix = guildConfig.CommandPrefix;
                    }

                    await _communicator.SendMessage(message.Channel, _helpBuilder.GetHelpEmbed(prefix, prefix != _botOptions.Value.DefaultCommandPrefix).Build());
                });
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _client.MessageReceived -= HandleMessageReceived;
        }
    }
}
