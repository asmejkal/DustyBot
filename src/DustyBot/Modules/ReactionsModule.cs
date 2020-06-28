﻿using Discord;
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
    [Module("Reactions", "Automatic reactions to messages and custom commands.")]
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

        [Command("reactions", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("reactions"), Alias("reaction"), Alias("reaction", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("reactions", "add", "Adds a reaction.")]
        [Alias("reaction", "add")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("Trigger", ParameterType.String)]
        [Parameter("Response", ParameterType.String, ParameterFlags.Remainder)]
        [Example("\"hi bot\" beep boop")]
        public async Task ReactionsAdd(ICommand command)
        {
            var id = await Settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                var newId = s.NextReactionId++;
                s.Reactions.Add(new Reaction() { Id = newId, Trigger = command["Trigger"], Value = command["Response"] });
                return newId;
            });

            await command.ReplySuccess(Communicator, $"Reaction `{id}` added!");
        }

        [Command("reactions", "remove", "Removes a reaction.")]
        [Alias("reaction", "remove")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("ID", ParameterType.Int)]
        [Comment("Use `reactions list` to see all reactions and their IDs.")]
        public async Task ReactionsRemove(ICommand command)
        {
            var result = await Settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                return s.Reactions.RemoveAll(x => x.Id == (int)command[0]) > 0;
            }).ConfigureAwait(false);
            
            if (!result)
                await command.ReplyError(Communicator, "Could not find a reaction with this ID.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, "Reaction removed.").ConfigureAwait(false);
        }

        [Command("reactions", "clear", "Removes all reactions.")]
        [Alias("reaction", "clear")]
        [Permissions(GuildPermission.ManageMessages)]
        public async Task ReactionsClear(ICommand command)
        {
            await Settings.Modify(command.GuildId, (ReactionsSettings s) =>
            {
                s.Reset();
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "All reactions cleared.").ConfigureAwait(false);
        }

        [Command("reactions", "list", "Lists all reactions.")]
        [Alias("reaction", "list")]
        public async Task ReactionsList(ICommand command)
        {
            var pages = new PageCollection();
            int count = 0;
            foreach (var reaction in (await Settings.Read<ReactionsSettings>(command.GuildId)).Reactions)
            {
                if (count++ % 20 == 0)
                    pages.Add(new EmbedBuilder().WithTitle("Reactions").WithDescription(string.Empty));

                pages.Last.Embed.Description += $"`{reaction.Id}.` {reaction.Trigger}\n";
            }

            if (pages.Count <= 0)
            {
                await command.Reply(Communicator, "No reactions have been set up.").ConfigureAwait(false);
                return;
            }

            await command.Reply(Communicator, pages).ConfigureAwait(false);
        }

        public override Task OnMessageReceived(SocketMessage message)
        {
            TaskHelper.FireForget(async () =>
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

                    var reaction = settings.GetRandom(message.Content);
                    if (reaction == null)
                        return;

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Reactions", $"\"{message.Content}\" by {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}"));

                    await Communicator.SendMessage(channel, reaction);
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Reactions", "Failed to process reaction", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
