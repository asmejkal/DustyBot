using Discord;
using System;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Settings;
using DustyBot.Helpers;
using Discord.WebSocket;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using DustyBot.Core.Formatting;
using DustyBot.Core.Parsing;
using System.Linq;
using System.Collections.Generic;
using DustyBot.Framework.Exceptions;
using System.IO;
using System.Globalization;
using Newtonsoft.Json;
using System.Net;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using Microsoft.Extensions.Logging;

namespace DustyBot.Modules
{
    [Module("Reactions", "Automatic reactions to messages and custom commands.")]
    internal sealed class ReactionsModule : IDisposable
    {
        private readonly BaseSocketClient _client;
        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly ILogger<ReactionsModule> _logger;
        private readonly IFrameworkReflector _frameworkReflector;

        public ReactionsModule(BaseSocketClient client, ICommunicator communicator, ISettingsService settings, ILogger<ReactionsModule> logger, IFrameworkReflector frameworkReflector)
        {
            _client = client;
            _communicator = communicator;
            _settings = settings;
            _logger = logger;
            _frameworkReflector = frameworkReflector;

            _client.MessageReceived += HandleMessageReceived;
        }

        [Command("reactions", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("reactions"), Alias("reaction"), Alias("reaction", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(HelpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("reactions", "add", "Adds a reaction.")]
        [Alias("reaction", "add")]
        [Parameter("Trigger", ParameterType.String)]
        [Parameter("Response", ParameterType.String, ParameterFlags.Remainder)]
        [Comment("If you add multiple reactions with the same trigger, one will be randomly chosen each time.")]
        [Example("\"hi bot\" beep boop")]
        public async Task AddReaction(ICommand command)
        {
            await AssertPrivileges(command.Author, command.GuildId);

            var id = await _settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                var newId = s.NextReactionId++;
                s.Reactions.Add(new Reaction() { Id = newId, Trigger = command["Trigger"], Value = command["Response"] });
                return newId;
            });

            await command.ReplySuccess($"Reaction `{id}` added!");
        }

        [Command("reactions", "edit", "Edits a reaction.")]
        [Alias("reaction", "edit")]
        [Parameter("IdOrTrigger", ParameterType.String, "the reaction trigger or ID from `reactions list`")]
        [Parameter("Response", ParameterType.String, ParameterFlags.Remainder, "the new response")]
        public async Task EditReaction(ICommand command)
        {
            await AssertPrivileges(command.Author, command.GuildId);

            var id = await _settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                var reaction = FindSingleReaction(s, command["IdOrTrigger"]);
                reaction.Value = command["Response"];
                return reaction.Id;
            });

            await command.ReplySuccess($"Reaction `{id}` edited!");
        }

        [Command("reactions", "rename", "Changes the trigger of a reaction.")]
        [Alias("reaction", "rename")]
        [Parameter("IdOrTrigger", ParameterType.String, "the reaction trigger or ID from `reactions list`")]
        [Parameter("Trigger", ParameterType.String, ParameterFlags.Remainder, "the new trigger")]
        public async Task RenameReaction(ICommand command)
        {
            await AssertPrivileges(command.Author, command.GuildId);

            var count = await _settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                var reactions = FindReactions(s, command["IdOrTrigger"]);
                foreach (var reaction in reactions)
                    reaction.Trigger = command["Trigger"];

                return reactions.Count;
            });

            await command.ReplySuccess($"Edited {count} reactions!");
        }

        [Command("reactions", "remove", "Removes a reaction.")]
        [Alias("reaction", "remove")]
        [Parameter("IdOrTrigger", ParameterType.String, ParameterFlags.Remainder, "the reaction trigger or ID from `reactions list`")]
        public async Task RemoveReaction(ICommand command)
        {
            await AssertPrivileges(command.Author, command.GuildId);

            var count = await _settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                var reactions = FindReactions(s, command["IdOrTrigger"]);
                s.Reactions = s.Reactions.Except(reactions).ToList();
                return reactions.Count;
            });
            
            await command.ReplySuccess($"Removed {count} reactions.");
        }

        [Command("reactions", "clear", "Removes all reactions.")]
        [Alias("reaction", "clear")]
        [Permissions(GuildPermission.Administrator)]
        public async Task ClearReactions(ICommand command)
        {
            await _settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                s.Reset();
            });

            await command.ReplySuccess("All reactions cleared.");
        }

        [Command("reactions", "list", "Lists all reactions.")]
        [Alias("reaction", "list", true)]
        public async Task ListReactions(ICommand command)
        {
            var settings = await _settings.Read<ReactionsSettings>(command.GuildId, false);
            if (settings != null)
            {
                var pages = BuildReactionList(settings.Reactions, "All reactions", footer: $"{settings.Reactions.Count} reactions");
                if (pages.Any())
                {
                    await command.Reply(pages, true);
                    return;
                }
            }

            await command.Reply("No reactions have been set up on this server.");
        }

        [Command("reactions", "search", "Shows all reactions containing a given word.")]
        [Alias("reaction", "search")]
        [Parameter("SearchString", ParameterType.String, ParameterFlags.Remainder, "one or more words of the trigger or response")]
        public async Task SearchReactions(ICommand command)
        {
            var settings = await _settings.Read<ReactionsSettings>(command.GuildId, false);
            if (settings != null)
            {
                var matches = settings.Reactions.Where(x => x.Trigger.Search(command["SearchString"], true) || x.Value.Search(command["SearchString"], true)).ToList();
                var pages = BuildReactionList(matches, $"Found {matches.Count} reaction{(matches.Count != 1 ? "s" : "")}");
                if (pages.Any())
                {
                    await command.Reply(pages, true);
                    return;
                }
            }

            await command.Reply($"Found no reactions containing `{command["SearchString"]}`.");
        }

        [Command("reactions", "stats", "Shows how many times reactions have been used.")]
        [Alias("reaction", "stats", true), Alias("reactions", "top")]
        [Parameter("IdOrTrigger", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "the reaction trigger or ID; shows all reactions if omitted")]
        public async Task ShowReactionStats(ICommand command)
        {
            var settings = await _settings.Read<ReactionsSettings>(command.GuildId, false);
            if (settings == null || !settings.Reactions.Any())
            {
                await command.Reply("No reactions have been set up on this server.");
                return;
            }

            IEnumerable<Reaction> reactions;
            if (command["IdOrTrigger"].HasValue)
                reactions = FindReactions(settings, command["IdOrTrigger"]);
            else
                reactions = settings.Reactions;

            var stats = reactions
                .GroupBy(x => x.Trigger)
                .Select(x => (Trigger: x.Key, TriggerCount: x.Sum(y => y.TriggerCount)))
                .OrderByDescending(x => x.TriggerCount)
                .ThenBy(x => x.Trigger);

            var pages = new PageCollectionBuilder();
            var place = 1;
            foreach (var reaction in stats.Take(100))
                pages.AppendLine($"`#{place++}` **{reaction.Trigger.Truncate(30)}** – triggered **{reaction.TriggerCount}** time{(reaction.TriggerCount != 1 ? "s" : "")}");

            var embedFactory = new Func<EmbedBuilder>(() => new EmbedBuilder()
                .WithTitle("Reaction statistics")
                .WithFooter($"{settings.Reactions.Count} reactions in total"));

            await command.Reply(pages.BuildEmbedCollection(embedFactory, 10), true);
        }

        [Command("reactions", "export", "Exports all reactions into a file.")]
        [Alias("reaction", "export", true)]
        public async Task ExportReactions(ICommand command)
        {
            await AssertPrivileges(command.Author, command.GuildId);

            var settings = await _settings.Read<ReactionsSettings>(command.GuildId, false);
            if (settings == null || !settings.Reactions.Any())
            {
                await command.Reply("No reactions have been set up on this server.");
                return;
            }

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                var reactions = settings.Reactions.Select(x => new Dictionary<string, string>() { { x.Trigger, x.Value } });
                writer.Write(JsonConvert.SerializeObject(reactions, Formatting.Indented));
                writer.Flush();
                stream.Position = 0;

                await command.Channel.SendFileAsync(stream, $"Reactions-{command.Guild.Name}-{DateTime.UtcNow.ToString("yyMMdd-HH-mm", CultureInfo.InvariantCulture)}.json", $"Exported {settings.Reactions.Count} reactions!");
            }
        }

        [Command("reactions", "import", "Adds all reactions from a file.")]
        [Alias("reaction", "import", true)]
        [Comment("Attach a file with reactions obtained with `reactions export`. Expected format: \n`[{\"trigger1\":\"value1\"},{\"trigger2\":\"value2\"}]` \n\nAlternative format (doesn't allow duplicates): \n`{\"trigger1\":\"value1\",\"trigger2\":\"value2\"}`")]
        public async Task ImportReactions(ICommand command)
        {
            await AssertPrivileges(command.Author, command.GuildId);

            if (!command.Message.Attachments.Any())
                throw new IncorrectParametersCommandException("You need to attach a file with reactions obtained with `reactions export`.", true);

            var attachment = command.Message.Attachments.First();
            var request = WebRequest.CreateHttp(attachment.Url);
            using (var response = await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();
                ICollection<Dictionary<string, string>> reactions;
                try
                {
                    reactions = JsonConvert.DeserializeObject<ICollection<Dictionary<string, string>>>(content);
                }
                catch (JsonException)
                {
                    try
                    {
                        // Try the second format
                        reactions = new[] { JsonConvert.DeserializeObject<Dictionary<string, string>>(content) };
                    }
                    catch (JsonException)
                    {
                        throw new IncorrectParametersCommandException("The provided file is invalid.", true);
                    }
                }

                await _settings.Modify(command.GuildId, (ReactionsSettings s) =>
                {
                    foreach (var reaction in reactions.SelectMany(x => x))
                    {
                        var newId = s.NextReactionId++;
                        s.Reactions.Add(new Reaction() { Id = newId, Trigger = reaction.Key, Value = reaction.Value });
                    }
                });

                await command.ReplySuccess($"Added {reactions.Count} reactions!");
            }
        }

        [Command("reactions", "set", "manager", "Sets an optional role that allows users to manage the reactions.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Optional | ParameterFlags.Remainder)]
        [Comment("Users with this role will be able to manage the reactions, in addition to users with the Manage Messages privilege.\n\nUse without parameters to disable.")]
        public async Task SetManagerRole(ICommand command)
        {
            var r = await _settings.Modify(command.GuildId, (ReactionsSettings s) => s.ManagerRole = command["RoleNameOrID"].AsRole?.Id ?? default);
            if (r == default)
                await command.ReplySuccess($"Reactions manager role has been disabled. Users with the Manage Messages permission can still edit the reactions.");
            else
                await command.ReplySuccess($"Users with role `{command["RoleNameOrID"].AsRole.Name}` (`{command["RoleNameOrID"].AsRole.Id}`) will now be allowed to manage reactions.");
        }

        private Task HandleMessageReceived(SocketMessage message)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var channel = message.Channel as SocketTextChannel;
                    if (channel == null)
                        return;

                    var user = message.Author as IGuildUser;
                    if (user == null)
                        return;

                    if (user.IsBot)
                        return;

                    if (!channel.Guild.CurrentUser.GetPermissions(channel).SendMessages)
                        return;

                    var settings = await _settings.Read<ReactionsSettings>(channel.Guild.Id, false);
                    if (settings == null)
                        return;

                    var reaction = GetRandom(settings.Reactions, message.Content);
                    if (reaction == null)
                        return;

                    _logger.WithScope(message).LogInformation("Triggered reaction {ReactionTrigger} (id: {ReactionId})", message.Content, reaction.Id);

                    await _communicator.SendMessage(channel, reaction.Value);

                    await _settings.Modify(channel.Guild.Id, (ReactionsSettings x) => x.Reactions.First(x => x.Id == reaction.Id).TriggerCount++);
                }
                catch (Exception ex)
                {
                    _logger.WithScope(message).LogError(ex, "Failed to process potential reaction trigger");
                }
            });

            return Task.CompletedTask;
        }

        private async Task AssertPrivileges(IUser user, ulong guildId)
        {
            if (user is IGuildUser gu)
            {
                if (gu.GuildPermissions.ManageMessages)
                    return;

                var s = await _settings.Read<ReactionsSettings>(guildId);
                if (s.ManagerRole == default || !gu.RoleIds.Contains(s.ManagerRole))
                    throw new MissingPermissionsException("You may bypass this requirement by asking for a reactions manager role.", GuildPermission.ManageMessages);
            }
            else
                throw new InvalidOperationException();
        }

        private static Reaction GetRandom(ICollection<Reaction> reactions, string trigger)
        {
            var filtered = reactions.Where(x => string.Compare(x.Trigger, trigger, true) == 0).ToList();
            if (filtered.Count <= 0)
                return null;

            return filtered[new Random().Next(filtered.Count)];
        }

        private static IReadOnlyCollection<Reaction> FindReactions(ReactionsSettings settings, string idOrTrigger)
        {
            if (idOrTrigger.All(x => char.IsDigit(x)) && int.TryParse(idOrTrigger, out var id))
            {
                var reaction = settings.Reactions.FirstOrDefault(x => x.Id == id);
                if (reaction != null)
                    return new[] { reaction };
            }

            var reactions = settings.Reactions.Where(x => string.Compare(x.Trigger, idOrTrigger, true) == 0).ToList();
            if (!reactions.Any())
                throw new IncorrectParametersCommandException($"Can't find a reaction with ID or trigger `{idOrTrigger}`");

            return reactions;
        }

        private static Reaction FindSingleReaction(ReactionsSettings settings, string idOrTrigger)
        {
            var reactions = FindReactions(settings, idOrTrigger);
            if (reactions.Count > 1)
                throw new AbortException(BuildReactionList(reactions, "Multiple matches", footer: "Please pick one and run the command again with its ID number"));

            return reactions.Single();
        }

        private static PageCollection BuildReactionList(IEnumerable<Reaction> reactions, string title, string footer = null)
        {
            var pages = new PageCollection();
            int count = 0;
            foreach (var reaction in reactions.OrderBy(x => x.Trigger).ThenBy(x => x.Id))
            {
                if (count++ % 10 == 0)
                {
                    var embed = new EmbedBuilder().WithTitle(title);
                    if (!string.IsNullOrEmpty(footer))
                        embed.WithFooter(footer);

                    pages.Add(embed);
                }

                pages.Last.Embed.AddField(x => x
                    .WithName($"{reaction.Id}: {reaction.Trigger}".Truncate(EmbedBuilder.MaxTitleLength))
                    .WithValue(reaction.Value.Truncate(500)));
            }

            return pages;
        }

        public void Dispose()
        {
            _client.MessageReceived -= HandleMessageReceived;
        }
    }
}
