﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Service.Helpers;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Modules
{
    [Module("Greet & bye", "Greet & bye messages.")]
    internal sealed class EventsModule : IDisposable
    {
        public const string MentionPlaceholder = "{mention}";
        public const string NamePlaceholder = "{name}";
        public const string FullNamePlaceholder = "{fullname}";
        public const string IdPlaceholder = "{id}";
        public const string ServerPlaceholder = "{server}";
        public const string MemberCountPlaceholder = "{membercount}";

        private readonly BaseSocketClient _client;
        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly ILogger<EventsModule> _logger;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;

        public EventsModule(
            BaseSocketClient client, 
            ICommunicator communicator, 
            ISettingsService settings, 
            ILogger<EventsModule> logger, 
            IFrameworkReflector frameworkReflector,
            HelpBuilder helpBuilder)
        {
            _client = client;
            _communicator = communicator;
            _settings = settings;
            _logger = logger;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;

            _client.UserJoined += HandleUserJoined;
            _client.UserLeft += HandleUserLeft;
        }

        [Command("greet", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("greet"), Alias("bye", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("greet", "text", "Sets a text greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, "a channel that will receive the messages")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the greeting message; you can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + ", " + ServerPlaceholder + ", and " + MemberCountPlaceholder + " placeholders")]
        public async Task Greet(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await _settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = command["Channel"].AsTextChannel.Id;
                s.GreetMessage = command["Message"];
            });

            await command.ReplySuccess("Greeting message set.");
        }

        [Command("greet", "embed", "Sets an embed greeting message.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, "a channel that will receive the messages")]
        [Parameter("Color", ParameterType.ColorCode, ParameterFlags.Optional, "hex code of a color (e.g. `#09A5BC`)")]
        [Parameter("Title", ParameterType.String, "title of the message")]
        [Parameter("Body", ParameterType.String, ParameterFlags.Remainder, "body of the greeting message")]
        [Comment("You can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + ", " + ServerPlaceholder + ", and " + MemberCountPlaceholder + " placeholders.\nYou can also **attach an image** or gif to display it in the message (don't delete the command after, it also deletes the picture).")]
        [Example("#general #09A5BC \"Welcome to {server}, {name}!\"\nHello, {mention}.\nDon't forget to check out the #rules!")]
        public async Task GreetEmbed(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await _settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = command["Channel"].AsTextChannel.Id;
                s.GreetEmbed = new GreetEmbed(command["Title"], command["Body"]);
                
                if (command["Color"].AsColorCode.HasValue)
                    s.GreetEmbed.Color = command["Color"].AsColorCode;

                if (command.Message.Attachments.Any())
                    s.GreetEmbed.Image = new Uri(command.Message.Attachments.First().Url);
            });

            await command.ReplySuccess("Greeting message set.");
        }

        [Command("greet", "embed", "set", "footer", "Customize a footer for your greet embed.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Text", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "the footer text")]
        [Comment("You can use " + MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + ", " + ServerPlaceholder + ", and " + MemberCountPlaceholder + " placeholders. \nUse without parameters to hide the footer.")]
        [Example("Member #{membercount}")]
        public async Task SetGreetEmbedFooter(ICommand command)
        {
            var footer = command["Text"].AsString;
            var set = !string.IsNullOrEmpty(footer);
            await _settings.Modify(command.GuildId, (EventsSettings s) =>
            {
                if (s.GreetEmbed == null)
                    throw new CommandException("You have to set a greet embed first!");

                s.GreetEmbed.Footer = set ? footer : null;
            });

            await command.ReplySuccess(set ? "Greet embed footer has been set." : "Greet embed footer is now hidden.");
        }

        [Command("greet", "disable", "Disables greeting messages.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task GreetDisable(ICommand command)
        {
            await _settings.Modify(command.GuildId, (EventsSettings s) => s.ResetGreet());
            await command.ReplySuccess("Greeting has been disabled.");
        }

        [Command("greet", "test", "Sends a sample greet message in this channel.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task GreetTest(ICommand command)
        {
            var settings = await _settings.Read<EventsSettings>(command.GuildId, false);
            if (settings == null || settings.GreetChannel == default)
            {
                await command.ReplyError("No greeting has been set.");
                return;
            }

            await Greet(command.Message.Channel, settings, (SocketGuildUser)command.Message.Author);
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
                await _settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = 0;
                    s.ByeMessage = string.Empty;
                });

                await command.ReplySuccess("Bye message has been disabled.");
            }
            else if (command.ParametersCount >= 2)
            {
                if (!await command["Channel"].IsType(ParameterType.TextChannel))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a text channel as first paramter.");

                if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
                {
                    await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                    return;
                }

                await _settings.Modify(command.GuildId, (EventsSettings s) =>
                {
                    s.ByeChannel = command["Channel"].AsTextChannel.Id;
                    s.ByeMessage = command["Message"];
                });

                await command.ReplySuccess("Bye message set.");
            }
            else
            {
                throw new IncorrectParametersCommandException(string.Empty);
            }
        }

        public void Dispose()
        {
            _client.UserJoined -= HandleUserJoined;
            _client.UserLeft -= HandleUserLeft;
        }

        private string ReplacePlaceholders(string message, SocketGuildUser user)
        {
            return message.Replace(MentionPlaceholder, user.Mention)
                .Replace(NamePlaceholder, user.Username)
                .Replace(FullNamePlaceholder, user.Username + "#" + user.Discriminator)
                .Replace(IdPlaceholder, user.Id.ToString())
                .Replace(ServerPlaceholder, user.Guild.Name)
                .Replace(MemberCountPlaceholder, user.Guild.MemberCount.ToString());
        }

        private async Task Greet(IMessageChannel channel, EventsSettings settings, SocketGuildUser user)
        {
            if (settings.GreetMessage != default)
            {
                await _communicator.SendMessage(channel, ReplacePlaceholders(settings.GreetMessage, user));
            }
            else if (settings.GreetEmbed != default)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(ReplacePlaceholders(settings.GreetEmbed.Title, user))
                    .WithDescription(ReplacePlaceholders(settings.GreetEmbed.Body, user))
                    .WithThumbnailUrl(user.GetAvatarUrl(size: 512) ?? user.GetDefaultAvatarUrl());

                if (settings.GreetEmbed.Color.HasValue)
                    embed.WithColor(Math.Min(settings.GreetEmbed.Color.Value, 0xfffffe)); // 0xfffffff is a special code for blank...

                if (settings.GreetEmbed.Image != default)
                    embed.WithImageUrl(settings.GreetEmbed.Image.AbsoluteUri);

                if (!string.IsNullOrEmpty(settings.GreetEmbed.Footer))
                    embed.WithFooter(ReplacePlaceholders(settings.GreetEmbed.Footer, user));

                await _communicator.SendMessage(channel, embed.Build());
            }
            else
            {
                throw new InvalidOperationException("Inconsistent settings");
            }
        }

        private async Task<bool> ProcessAutoban(SocketGuildUser guildUser)
        {
            var settings = await _settings.Read<AdministrationSettings>(guildUser.Guild.Id, false);
            if (settings == null)
                return false;

            if (settings.AutobanUsernameRegex == default || settings.AutobanLogChannelId == default)
                return false;

            var logger = _logger.WithScope(guildUser);
            var currentUser = guildUser.Guild.CurrentUser;
            if (!currentUser.GuildPermissions.BanMembers)
            {
                logger.LogInformation("Missing permissions to process autoban");
                return false;
            }

            var channel = guildUser.Guild.GetTextChannel(settings.AutobanLogChannelId);
            if (channel == null || !currentUser.GetPermissions(channel).SendMessages)
            {
                logger.LogInformation("Can't log autobans");
                return false;
            }

            try
            {
                if (!Regex.IsMatch(guildUser.Username, settings.AutobanUsernameRegex, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
                    return false;
            }
            catch (RegexMatchTimeoutException ex)
            {
                logger.LogWarning(ex, "Failed to process autoban because of regex timeout");
                return false;
            }
            
            await guildUser.BanAsync(1, "autobanned");

            var embed = new EmbedBuilder()
                .WithFooter($"ID: {guildUser.Id}")
                .WithDescription($"**Autobanned user {guildUser.Mention}:**\n Username `{guildUser.Username}` matches the autoban rule.").WithColor(Color.Red);

            await channel.SendMessageAsync("", embed: embed.Build());

            logger.LogInformation("Autobanned user");
            return true;
        }

        private Task HandleUserJoined(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    try
                    {
                        if (await ProcessAutoban(guildUser))
                        {
                            // Skip greeting
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.WithScope(guildUser).LogError(ex, "Failed to process autoban");
                    }

                    var settings = await _settings.Read<EventsSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (settings.GreetChannel == default)
                        return;

                    var channel = guildUser.Guild.GetTextChannel(settings.GreetChannel);
                    if (channel == null)
                        return;

                    var logger = _logger.WithScope(channel).WithScope(guildUser);
                    if (!guildUser.Guild.CurrentUser.GetPermissions(channel).SendMessages)
                    {
                        logger.LogInformation("Can't greet user because of missing permissions");
                        return;
                    }

                    logger.LogInformation("Greeting user");

                    await Greet(channel, settings, guildUser);
                }
                catch (Exception ex)
                {
                    _logger.WithScope(guildUser).LogError(ex, "Failed to process greeting event");
                }
            });

            return Task.CompletedTask;
        }

        private Task HandleUserLeft(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await _settings.Read<EventsSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (settings.ByeChannel == default || string.IsNullOrWhiteSpace(settings.ByeMessage))
                        return;

                    var channel = guildUser.Guild.GetTextChannel(settings.ByeChannel);
                    if (channel == null)
                        return;

                    var logger = _logger.WithScope(channel).WithScope(guildUser);
                    if (!guildUser.Guild.CurrentUser.GetPermissions(channel).SendMessages)
                    {
                        logger.LogInformation("Can't bye user because of missing permissions");
                        return;
                    }

                    logger.LogInformation("Goodbyed user");

                    await _communicator.SendMessage(channel, ReplacePlaceholders(settings.ByeMessage, guildUser));
                }
                catch (Exception ex)
                {
                    _logger.WithScope(guildUser).LogError(ex, "Failed to process bye event");
                }
            });

            return Task.CompletedTask;
        }
    }
}
