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
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "a channel that will receive the messages")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "the greeting message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + " and " + IdPlaceholder + " placeholders")]
        [Comment("Use without parameters to disable the greeting message.")]
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
            else if (command.ParametersCount >= 2)
            {
                if (!await command[0].IsType(ParameterType.TextChannel))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a text channel as first paramter.");

                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.GreetChannel = command[0].AsTextChannel.Id;
                    s.GreetMessage = command.Remainder.After(1);
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, "Greeting message set.").ConfigureAwait(false);
            }
            else
                throw new Framework.Exceptions.IncorrectParametersCommandException(string.Empty);
        }

        [Command("bye", "Sets or disables a goodbye message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "a channel that will receive the messages")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "the bye message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + " and " + IdPlaceholder + " placeholders")]
        [Comment("Use without parameters to disable the bye message.")]
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
            else if (command.ParametersCount >= 2)
            {
                if (!await command[0].IsType(ParameterType.TextChannel))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a text channel as first paramter.");

                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = command[0].AsTextChannel.Id;
                    s.ByeMessage = command.Remainder.After(1);
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, "Bye message set.").ConfigureAwait(false);
            }
            else
                throw new Framework.Exceptions.IncorrectParametersCommandException(string.Empty);
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
