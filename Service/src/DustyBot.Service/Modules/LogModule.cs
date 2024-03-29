﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Service.Helpers;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Modules
{
    [Module("Log", "Log deleted messages and other events.")]
    internal sealed class LogModule : IDisposable
    {
        private readonly BaseSocketClient _client;
        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly ILogger<LogModule> _logger;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;

        public LogModule(
            BaseSocketClient client, 
            ICommunicator communicator, 
            ISettingsService settings, 
            ILogger<LogModule> logger, 
            IFrameworkReflector frameworkReflector,
            HelpBuilder helpBuilder)
        {
            _client = client;
            _communicator = communicator;
            _settings = settings;
            _logger = logger;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;

            _client.MessageDeleted += HandleMessageDeleted;
            _client.MessagesBulkDeleted += HandleMessagesBulkDeleted;
        }

        [Command("log", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("log")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("log", "messages", "Sets a channel for logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel)]
        public async Task LogMessages(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await _settings.Modify(command.GuildId, (LogSettings s) => s.EventMessageDeletedChannel = command["Channel"].AsTextChannel.Id);
            await command.ReplySuccess($"Deleted messages will now be logged in the {command["Channel"].AsTextChannel.Mention} channel.");
        }

        [Command("log", "messages", "disable", "Disables logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task LogMessagesDisable(ICommand command)
        {
            await _settings.Modify(command.GuildId, (LogSettings s) => s.EventMessageDeletedChannel = 0);
            await command.ReplySuccess($"Logging of deleted messages has been disabled.");
        }

        [Command("log", "filter", "messages", "Sets or disables a regex filter for deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RegularExpression", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "Messages that match this regular expression won't be logged.")]
        [Comment("Use without parameters to disable. For testing of regular expressions you can use https://regexr.com/.")]
        public async Task SetMessagesFilter(ICommand command)
        {
            await _settings.Modify(command.GuildId, (LogSettings s) => s.EventMessageDeletedFilter = command["RegularExpression"]);
            await command.ReplySuccess(string.IsNullOrEmpty(command["RegularExpression"]) ? "Filtering of deleted messages has been disabled." : "A filter for logged deleted messages has been set.");
        }

        [Command("log", "filter", "channels", "Excludes channels from logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channels", ParameterType.TextChannel, ParameterFlags.Optional | ParameterFlags.Repeatable, "one or more channels")]
        [Comment("Use without parameters to disable.")]
        [Example("#roles #welcome")]
        public async Task SetMessagesChannelFilter(ICommand command)
        {
            var channelIds = command["Channels"].Repeats.Select(x => x.AsTextChannel?.Id ?? 0).Where(x => x != 0).ToList();
            await _settings.Modify(command.GuildId, (LogSettings s) =>
            {
                s.EventMessageDeletedChannelFilter = channelIds;
            });

            await command.ReplySuccess("A channel filter for logging of deleted messages has been " + 
                (channelIds.Count > 0 ? "set." : "disabled."));
        }

        public void Dispose()
        {
            _client.MessageDeleted -= HandleMessageDeleted;
            _client.MessagesBulkDeleted -= HandleMessagesBulkDeleted;
        }

        private Task HandleMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var userMessage = (message.HasValue ? message.Value : null) as IUserMessage;
                    if (userMessage == null)
                        return;

                    if (userMessage.Author.IsBot)
                        return;

                    var textChannel = channel as ITextChannel;
                    if (textChannel == null)
                        return;

                    var guild = textChannel.Guild as SocketGuild;
                    if (guild == null)
                        return;

                    var settings = await _settings.Read<LogSettings>(guild.Id, false);
                    if (settings == null)
                        return;

                    var eventChannelId = settings.EventMessageDeletedChannel;
                    if (eventChannelId == 0)
                        return;

                    var eventChannel = guild.TextChannels.FirstOrDefault(x => x.Id == eventChannelId);
                    if (eventChannel == null)
                        return;

                    if (!guild.CurrentUser.GetPermissions(eventChannel).SendMessages)
                    {
                        _logger.WithScope(userMessage).LogInformation("Didn't log deleted message because of missing permissions");
                        return;
                    }

                    if (settings.EventMessageDeletedChannelFilter.Contains(channel.Id))
                        return;

                    var filter = settings.EventMessageDeletedFilter;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(filter) && Regex.IsMatch(userMessage.Content, filter, RegexOptions.None, TimeSpan.FromSeconds(3)))
                            return;
                    }
                    catch (ArgumentException)
                    {
                        await _communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex is malformed.");
                        return;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        await _communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex takes too long to evaluate.");
                        return;
                    }

                    await LogSingleMessage(userMessage, textChannel, eventChannel);
                }
                catch (Exception ex)
                {
                    _logger.WithScope(channel, message.Id).LogError(ex, "Failed to process deleted message");
                }
            });

            return Task.CompletedTask;
        }

        private Task HandleMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> cacheables, ISocketMessageChannel channel)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var textChannel = channel as ITextChannel;
                    if (textChannel == null)
                        return;

                    var guild = textChannel.Guild as SocketGuild;
                    if (guild == null)
                        return;

                    var messages = cacheables
                        .Where(x => x.HasValue)
                        .Select(x => x.Value)
                        .OfType<IUserMessage>()
                        .Where(x => !x.Author.IsBot)
                        .ToList();

                    if (!messages.Any())
                        return;

                    var settings = await _settings.Read<LogSettings>(guild.Id, false);
                    if (settings == null)
                        return;

                    var eventChannelId = settings.EventMessageDeletedChannel;
                    if (eventChannelId == 0)
                        return;

                    var eventChannel = guild.TextChannels.FirstOrDefault(x => x.Id == eventChannelId);
                    if (eventChannel == null)
                        return;

                    var logger = _logger.WithScope(channel);
                    if (!guild.CurrentUser.GetPermissions(eventChannel).SendMessages)
                    {
                        logger.LogInformation("Didn't log bulk deleted messages because of missing permissions");
                        return;
                    }

                    if (settings.EventMessageDeletedChannelFilter.Contains(channel.Id))
                        return;

                    var logs = new List<string>();
                    var filter = settings.EventMessageDeletedFilter;
                    try
                    {
                        foreach (var message in messages)
                        {
                            if (!string.IsNullOrWhiteSpace(filter) && Regex.IsMatch(message.Content, filter, RegexOptions.None, TimeSpan.FromSeconds(3)))
                                continue;

                            var sb = new StringBuilder();
                            sb.AppendLine($"**Message by {message.Author.Mention} in {textChannel.Mention} was deleted:**");

                            if (!string.IsNullOrWhiteSpace(message.Content))
                                sb.AppendLine(message.Content);

                            if (message.Attachments.Any())
                                sb.AppendLine(string.Join("\n", message.Attachments.Select(a => a.Url)));

                            sb.Append(message.Timestamp.ToUniversalTime().ToString(@"yyyy\/MM\/dd H:mm:ss UTC"));

                            if (sb.Length > EmbedBuilder.MaxDescriptionLength / 2)
                            {
                                try
                                {
                                    await LogSingleMessage(message, textChannel, eventChannel);
                                }
                                catch (Exception ex)
                                {
                                    logger.WithScope(message).LogError(ex, "Failed to log single deleted message in a bulk delete");
                                }

                                continue;
                            }
                            else
                            {
                                logs.Add(sb.ToString());
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                        await _communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex is malformed.");
                        logger.LogInformation("Didn't log bulk deleted messages because the message filter regex is malformed");
                        return;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        await _communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex takes too long to evaluate.");
                        logger.LogInformation("Didn't log bulk deleted messages because the message filter regex took too long to evaluate");
                        return;
                    }

                    logger.LogInformation("Logging {Count} deleted messages", logs.Count);

                    var embed = new EmbedBuilder();
                    var delimiter = "\n\n";
                    foreach (var log in logs)
                    {
                        var totalLength = log.Length + delimiter.Length;
                        if (totalLength > EmbedBuilder.MaxDescriptionLength)
                            throw new InvalidOperationException(); // Shouldn't happen

                        if ((embed.Description?.Length ?? 0) + totalLength > EmbedBuilder.MaxDescriptionLength)
                        {
                            await _communicator.SendMessage(eventChannel, embed.Build());
                            embed = new EmbedBuilder();
                        }
                        else
                        {
                            embed.Description += log + delimiter;
                        }
                    }

                    if (!string.IsNullOrEmpty(embed.Description))
                        await _communicator.SendMessage(eventChannel, embed.Build());
                }
                catch (Exception ex)
                {
                    _logger.WithScope(channel).LogError(ex, "Failed to process deleted messages");
                }
            });

            return Task.CompletedTask;
        }

        private async Task LogSingleMessage(IUserMessage userMessage, ITextChannel textChannel, IMessageChannel eventChannel)
        {
            _logger.WithScope(userMessage).LogInformation("Logging deleted message");
            var preface = $"**Message by {userMessage.Author.Mention} in {textChannel.Mention} was deleted:**\n";
            var embed = new EmbedBuilder()
                .WithDescription(preface + userMessage.Content.Truncate(EmbedBuilder.MaxDescriptionLength - preface.Length))
                .WithFooter(fb => fb.WithText($"{userMessage.Timestamp.ToUniversalTime().ToString(@"yyyy\/MM\/dd H:mm:ss UTC")} (deleted on {DateTime.Now.ToUniversalTime().ToString(@"yyyy\/MM\/dd H:mm:ss UTC")})"));

            if (userMessage.Attachments.Any())
                embed.AddField(efb => efb.WithName("Attachments").WithValue(string.Join(", ", userMessage.Attachments.Select(a => a.Url))).WithIsInline(false));

            await _communicator.SendMessage(eventChannel, embed.Build());
        }
    }
}
