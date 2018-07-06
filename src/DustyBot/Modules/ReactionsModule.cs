using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Config;
using DustyBot.Settings;
using DustyBot.Helpers;
using Discord.WebSocket;

namespace DustyBot.Modules
{
    [Module("Reactions", "Automatic reactions to messages.")]
    class ReactionsModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }
        public IEssentialConfig Config { get; private set; }

        public ReactionsModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger, IEssentialConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            Config = config;
        }

        [Command("reactions", "add", "Adds a reaction.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Usage("{p}reaction add \"Trigger\" \"Response\"\n\n__Example:__ {p}reaction add hi \"hi there\"")]
        public async Task ReactionsAdd(ICommand command)
        {
            var id = await Settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                var reaction = new Reaction() { Value = (string)command.GetParameter(1) };
                ReactionGroup group;
                if (s.Groups.TryGetValue((string)command.GetParameter(0), out group))
                    group.Add(reaction);
                else
                    s.Groups.Add((string)command.GetParameter(0), new ReactionGroup() { reaction } );

                return reaction.Id;
            });

            await command.ReplySuccess(Communicator, $"Reaction with ID `{id}` added!");
        }

        [Command("reactions", "remove", "Removes a reaction.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameters(ParameterType.String)]
        [Usage("{p}reaction remove ID\n\nUse `{p}reactions ids` to see all reactions and their IDs.")]
        public async Task ReactionsRemove(ICommand command)
        {
            Guid id;
            if (!Guid.TryParse((string)command.GetParameter(0), out id))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `reactions ids` to view all reactions and their IDs.");

            var result = await Settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                foreach (var group in s.Groups)
                {
                    if (group.Value.RemoveAll(x => x.Id == id) > 0)
                    {
                        if (group.Value.Count <= 0)
                            s.Groups.Remove(group.Key);

                        return true;
                    }
                }

                return false;
            }).ConfigureAwait(false);
            
            if (!result)
                await command.ReplyError(Communicator, "Could not find a reaction with this ID.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, "Reaction removed.").ConfigureAwait(false);
        }

        [Command("reactions", "list", "Lists all reactions.")]
        [Usage("{p}reactions list")]
        public async Task ReactionsList(ICommand command)
        {
            var pages = new PageCollection();
            int count = 0;
            foreach (var group in (await Settings.Read<ReactionsSettings>(command.GuildId)).Groups.OrderBy(x => x.Key))
            {
                if (count % 20 == 0)
                {
                    count = 0;
                    pages.Add(new EmbedBuilder().WithTitle("Reactions").WithDescription(string.Empty));
                }

                pages.Last.Embed.Description += group.Key + (group.Value.Count > 1 ? $" ({group.Value.Count} variants)\n" : "\n");
                count++;
            }

            if (pages.Count <= 0)
            {
                await command.Reply(Communicator, "No reactions have been set up.").ConfigureAwait(false);
                return;
            }

            await command.Reply(Communicator, pages, true).ConfigureAwait(false);
        }

        [Command("reactions", "ids", "Lists all reactions and their IDs.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}reactions ids")]
        public async Task Vote(ICommand command)
        {
            var pages = new PageCollection();
            int count = 0;
            foreach (var group in (await Settings.Read<ReactionsSettings>(command.GuildId)).Groups.OrderBy(x => x.Key))
            {
                foreach (var reaction in group.Value)
                {
                    if (count % 20 == 0)
                    {
                        count = 0;
                        pages.Add(new EmbedBuilder().WithTitle("Reactions").WithDescription(string.Empty));
                    }

                    pages.Last.Embed.Description += $"`{reaction.Id}` {group.Key}\n";
                    count++;
                }
            }

            if (pages.Count <= 0)
            {
                await command.Reply(Communicator, "No reactions have been set up.").ConfigureAwait(false);
                return;
            }

            await command.Reply(Communicator, pages, true).ConfigureAwait(false);
        }

        public override async Task OnMessageReceived(SocketMessage message)
        {
            try
            {
                var channel = message.Channel as ITextChannel;
                if (channel == null)
                    return;

                var user = message.Author as IGuildUser;
                if (user == null)
                    return;

                if (user.IsBot)
                    return;

                var settings = await Settings.Read<ReactionsSettings>(channel.GuildId, false);
                if (settings == null)
                    return;

                ReactionGroup group;
                if (!settings.Groups.TryGetValue(message.Content, out group))
                    return;
                
                await Logger.Log(new LogMessage(LogSeverity.Info, "Reactions", $"\"{message.Content}\" by {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}"));

                await Communicator.SendMessage(channel, group.GetRandom());
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Reactions", "Failed to process reaction", ex));
            }
        }
    }
}
