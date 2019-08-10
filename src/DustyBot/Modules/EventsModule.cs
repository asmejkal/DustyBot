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
        public const string ServerPlaceholder = "{server}";

        public EventsModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("greet", "Sets or disables a greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, "a channel that will receive the messages")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the greeting message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + " and " + ServerPlaceholder + " placeholders")]
        [Comment("Use without parameters to disable the greeting message.")]
        public async Task Greet(ICommand command)
        {
            await Settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = command["Channel"].AsTextChannel.Id;
                s.GreetMessage = command["Message"];
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "Greeting message set.").ConfigureAwait(false);
        }

        [Command("greet", "embed", "Sets an embed greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, "a channel that will receive the messages")]
        [Parameter("Title", ParameterType.String, "title of the message; you can use the placeholders listed below")]
        [Parameter("Image", ParameterType.Uri, ParameterFlags.Optional, "link to an image/gif to show in the embed")]
        [Parameter("Body", ParameterType.String, ParameterFlags.Remainder, "body of the greeting message; you can use the placeholders listed below")]
        [Comment("You can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + " and " + ServerPlaceholder + " placeholders.")]
        public async Task GreetEmbed(ICommand command)
        {
            await Settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = command["Channel"].AsTextChannel.Id;
                s.GreetEmbed = new GreetEmbed(command["Title"], command["Body"], command["Image"].AsUri);
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "Greeting message set.").ConfigureAwait(false);
        }

        [Command("greet", "disable", "Disables greeting messages.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task GreetDisable(ICommand command)
        {
            await Settings.Modify(command.GuildId, (EventsSettings s) => s.ResetGreet()).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, "Greeting has been disabled.").ConfigureAwait(false);
        }

        [Command("bye", "Sets or disables a goodbye message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "a channel that will receive the messages")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "the bye message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + " and " + ServerPlaceholder + " placeholders")]
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
                if (!await command["Channel"].IsType(ParameterType.TextChannel))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a text channel as first paramter.");

                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = command["Channel"].AsTextChannel.Id;
                    s.ByeMessage = command["Message"];
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
                .Replace(IdPlaceholder, user.Id.ToString())
                .Replace(ServerPlaceholder, user.Guild.Name);
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

                    if (settings.GreetChannel == default)
                        return;

                    var channel = guildUser.Guild.GetTextChannel(settings.GreetChannel);
                    if (channel == null)
                        return;

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Events", $"Greeting user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}"));

                    if (settings.GreetMessage != default)
                    {
                        await Communicator.SendMessage(channel, ReplacePlaceholders(settings.GreetMessage, guildUser));
                    }
                    else if (settings.GreetEmbed != default)
                    {
                        var embed = new EmbedBuilder()
                            .WithTitle(ReplacePlaceholders(settings.GreetEmbed.Title, guildUser))
                            .WithDescription(ReplacePlaceholders(settings.GreetEmbed.Body, guildUser))
                            .WithThumbnailUrl(guildUser.GetAvatarUrl());

                        if (settings.GreetEmbed.Image != default)
                            embed.WithImageUrl(settings.GreetEmbed.Image.AbsoluteUri);

                        await channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                        throw new InvalidOperationException("Inconsistent settings");
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

                    if (settings.ByeChannel == default || string.IsNullOrWhiteSpace(settings.ByeMessage))
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
