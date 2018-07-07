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
using DustyBot.Settings;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace DustyBot.Modules
{
    [Module("Events", "Reactions to server events.")]
    class EventsModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }

        public const string MentionPlaceholder = "{mention}";
        public const string NamePlaceholder = "{name}";
        public const string FullNamePlaceholder = "{fullname}";
        public const string IdPlaceholder = "{id}";

        public EventsModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("greet", "Sets or disables a greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}greet Channel Message...\n\n● *Channel* - a channel that will receive the messages\n● *Message* - the greeting message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + " and " + IdPlaceholder + " placeholders\n\nUse without parameters to disable the greeting message.")]
        public async Task Greet(ICommand command)
        {
            if (command.ParametersCount <= 0)
            {
                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.GreetChannel = 0;
                    s.GreetMessage = string.Empty;
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, "Greeting has been disabled.").ConfigureAwait(false);
            }
            else
            {
                if (command.ParametersCount < 2)
                    throw new Framework.Exceptions.IncorrectParametersCommandException(string.Empty);

                if (command[0].AsTextChannel == null)
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Please provide a channel.");

                var text = new string(command.Body.SkipWhile(c => !char.IsWhiteSpace(c)).ToArray());

                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.GreetChannel = command[0].AsTextChannel.Id;
                    s.GreetMessage = text;
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, "Greeting message set.").ConfigureAwait(false);
            }
        }

        [Command("bye", "Sets or disables a goodbye message.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}bye Channel Message...\n\n● *Channel* - a channel that will receive the messages\n● *Message* - the message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + " and " + IdPlaceholder + " placeholders\n\nUse without parameters to disable the message.")]
        public async Task Bye(ICommand command)
        {
            if (command.ParametersCount <= 0)
            {
                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = 0;
                    s.ByeMessage = string.Empty;
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, "Bye message has been disabled.").ConfigureAwait(false);
            }
            else
            {
                if (command.ParametersCount < 2)
                    throw new Framework.Exceptions.IncorrectParametersCommandException(string.Empty);

                if (command[0].AsTextChannel == null)
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Please provide a channel.");

                var text = new string(command.Body.SkipWhile(c => !char.IsWhiteSpace(c)).ToArray());

                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = command[0].AsTextChannel.Id;
                    s.ByeMessage = text;
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, "Bye message set.").ConfigureAwait(false);
            }
        }

        private string ReplacePlaceholders(string message, SocketGuildUser user)
        {
            return message.Replace(MentionPlaceholder, user.Mention)
                .Replace(NamePlaceholder, user.Username)
                .Replace(FullNamePlaceholder, user.Username + "#" + user.Discriminator)
                .Replace(IdPlaceholder, user.Id.ToString());
        }

        public override Task OnUserJoined(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await Settings.Read<EventsSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (settings.GreetChannel == default(ulong) || string.IsNullOrWhiteSpace(settings.GreetMessage))
                        return;

                    var channel = guildUser.Guild.GetTextChannel(settings.GreetChannel);
                    if (channel == null)
                        return;

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Events", $"Greeted user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}"));

                    await Communicator.SendMessage(channel, ReplacePlaceholders(settings.GreetMessage, guildUser));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Events", "Failed to process greeting event", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnUserLeft(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await Settings.Read<EventsSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (settings.ByeChannel == default(ulong) || string.IsNullOrWhiteSpace(settings.ByeMessage))
                        return;

                    var channel = guildUser.Guild.GetTextChannel(settings.ByeChannel);
                    if (channel == null)
                        return;

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Events", $"Goodbyed user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}"));

                    await Communicator.SendMessage(channel, ReplacePlaceholders(settings.ByeMessage, guildUser));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Events", "Failed to process bye event", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
