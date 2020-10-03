using Discord;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Settings;
using Discord.WebSocket;
using DustyBot.Helpers;
using DustyBot.Database.Services;
using DustyBot.Core.Async;

namespace DustyBot.Modules
{
    [Module("Greet & bye", "Greet & bye messages.")]
    class EventsModule : Module
    {
        public ICommunicator Communicator { get; }
        public ISettingsService Settings { get; }
        public ILogger Logger { get; }

        public const string MentionPlaceholder = "{mention}";
        public const string NamePlaceholder = "{name}";
        public const string FullNamePlaceholder = "{fullname}";
        public const string IdPlaceholder = "{id}";
        public const string ServerPlaceholder = "{server}";

        public EventsModule(ICommunicator communicator, ISettingsService settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("greet", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("greet"), Alias("bye", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("greet", "text", "Sets a text greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, "a channel that will receive the messages")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the greeting message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + " and " + ServerPlaceholder + " placeholders")]
        public async Task Greet(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await Settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = command["Channel"].AsTextChannel.Id;
                s.GreetMessage = command["Message"];
            });

            await command.ReplySuccess(Communicator, "Greeting message set.");
        }

        [Command("greet", "embed", "Sets an embed greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, "a channel that will receive the messages")]
        [Parameter("Color", ParameterType.ColorCode, ParameterFlags.Optional, "hex code of a color (e.g. `#09A5BC`)")]
        [Parameter("Title", ParameterType.String, "title of the message")]
        [Parameter("Body", ParameterType.String, ParameterFlags.Remainder, "body of the greeting message")]
        [Comment("You can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + " and " + ServerPlaceholder + " placeholders.\nYou can also **attach an image** or gif to display it in the message (don't delete the command after, it also deletes the picture).")]
        [Example("#general #09A5BC \"Welcome to {server}, {name}!\"\nHello, {mention}.\nDon't forget to check out the #rules!")]
        public async Task GreetEmbed(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await Settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = command["Channel"].AsTextChannel.Id;
                s.GreetEmbed = new GreetEmbed(command["Title"], command["Body"]);
                
                if (command["Color"].AsColorCode.HasValue)
                    s.GreetEmbed.Color = command["Color"].AsColorCode;

                if (command.Message.Attachments.Any())
                    s.GreetEmbed.Image = new Uri(command.Message.Attachments.First().Url);
            });

            await command.ReplySuccess(Communicator, "Greeting message set.");
        }

        [Command("greet", "disable", "Disables greeting messages.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task GreetDisable(ICommand command)
        {
            await Settings.Modify(command.GuildId, (EventsSettings s) => s.ResetGreet());
            await command.ReplySuccess(Communicator, "Greeting has been disabled.");
        }

        [Command("greet", "test", "Sends a sample greet message in this channel.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task GreetTest(ICommand command)
        {
            var settings = await Settings.Read<EventsSettings>(command.GuildId, false);
            if (settings == null || settings.GreetChannel == default)
            {
                await command.ReplyError(Communicator, "No greeting has been set.");
                return;
            }

            await Greet((ITextChannel)command.Message.Channel, settings, (IGuildUser)command.Message.Author);
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
                });

                await command.ReplySuccess(Communicator, "Bye message has been disabled.");
            }
            else if (command.ParametersCount >= 2)
            {
                if (!await command["Channel"].IsType(ParameterType.TextChannel))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a text channel as first paramter.");

                if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
                {
                    await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                    return;
                }

                await Settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = command["Channel"].AsTextChannel.Id;
                    s.ByeMessage = command["Message"];
                });

                await command.ReplySuccess(Communicator, "Bye message set.");
            }
            else
                throw new Framework.Exceptions.IncorrectParametersCommandException(string.Empty);
        }

        private string ReplacePlaceholders(string message, IGuildUser user)
        {
            return message.Replace(MentionPlaceholder, user.Mention)
                .Replace(NamePlaceholder, user.Username)
                .Replace(FullNamePlaceholder, user.Username + "#" + user.Discriminator)
                .Replace(IdPlaceholder, user.Id.ToString())
                .Replace(ServerPlaceholder, user.Guild.Name);
        }

        private async Task Greet(ITextChannel channel, EventsSettings settings, IGuildUser user)
        {
            if (settings.GreetMessage != default)
            {
                await Communicator.SendMessage(channel, ReplacePlaceholders(settings.GreetMessage, user));
            }
            else if (settings.GreetEmbed != default)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(ReplacePlaceholders(settings.GreetEmbed.Title, user))
                    .WithDescription(ReplacePlaceholders(settings.GreetEmbed.Body, user))
                    .WithThumbnailUrl(user.GetAvatarUrl(size: 512));

                if (settings.GreetEmbed.Color.HasValue)
                    embed.WithColor(settings.GreetEmbed.Color.Value);

                if (settings.GreetEmbed.Image != default)
                    embed.WithImageUrl(settings.GreetEmbed.Image.AbsoluteUri);

                await channel.SendMessageAsync(embed: embed.Build());
            }
            else
                throw new InvalidOperationException("Inconsistent settings");
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

                    await Greet(channel, settings, guildUser);
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
