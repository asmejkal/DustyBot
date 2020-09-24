using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Core.Utility;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using DustyBot.Framework.Logging;
using DustyBot.Helpers;

namespace DustyBot.Modules
{
    [Module("Log", "Provides logging of server events.")]
    class LogModule : Module
    {
        private ICommunicator Communicator { get; }
        private ISettingsProvider Settings { get; }
        private ILogger Logger { get; }
        private DiscordSocketClient SocketClient { get; }

        public LogModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger, DiscordSocketClient socketClient)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            SocketClient = socketClient;
        }

        [Command("log", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("log")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("log", "names", "Sets a channel for name change logging.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel)]
        public async Task LogNameChanges(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await Settings.Modify(command.GuildId, (LogSettings s) => s.EventNameChangedChannel = command["Channel"].AsTextChannel.Id).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Name changes will now be logged in the {command["Channel"].AsTextChannel.Mention} channel.").ConfigureAwait(false);
        }

        [Command("log", "names", "disable", "Disables name change logging.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task LogNameChangesDisable(ICommand command)
        {
            await Settings.Modify(command.GuildId, (LogSettings s) => s.EventNameChangedChannel = 0).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Name change logging channel has been disabled.").ConfigureAwait(false);
        }

        [Command("log", "messages", "Sets a channel for logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel)]
        public async Task LogMessages(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await Settings.Modify(command.GuildId, (LogSettings s) => s.EventMessageDeletedChannel = command["Channel"].AsTextChannel.Id).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Deleted messages will now be logged in the {command["Channel"].AsTextChannel.Mention} channel.").ConfigureAwait(false);
        }

        [Command("log", "messages", "disable", "Disables logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task LogMessagesDisable(ICommand command)
        {
            await Settings.Modify(command.GuildId, (LogSettings s) => s.EventMessageDeletedChannel = 0).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Logging of deleted messages has been disabled.").ConfigureAwait(false);
        }

        [Command("log", "filter", "messages", "Sets or disables a regex filter for deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RegularExpression", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "Messages that match this regular expression won't be logged.")]
        [Comment("Use without parameters to disable. For testing of regular expressions you can use https://regexr.com/.")]
        public async Task SetMessagesFilter(ICommand command)
        {
            await Settings.Modify(command.GuildId, (LogSettings s) => s.EventMessageDeletedFilter = command["RegularExpression"]).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, string.IsNullOrEmpty(command["RegularExpression"]) ? "Filtering of deleted messages has been disabled." : "A filter for logged deleted messages has been set.").ConfigureAwait(false);
        }

        [Command("log", "filter", "channels", "Excludes channels from logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channels", ParameterType.TextChannel, ParameterFlags.Optional | ParameterFlags.Repeatable, "one or more channels")]
        [Comment("Use without parameters to disable.")]
        [Example("#roles #welcome")]
        public async Task SetMessagesChannelFilter(ICommand command)
        {
            var channelIds = command["Channels"].Repeats.Select(x => x.AsTextChannel?.Id ?? 0).Where(x => x != 0).ToList();
            await Settings.Modify(command.GuildId, (LogSettings s) =>
            {
                s.EventMessageDeletedChannelFilter = channelIds;
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "A channel filter for logging of deleted messages has been " + 
                (channelIds.Count > 0 ? "set." : "disabled.")).ConfigureAwait(false);
        }

        public override Task OnGuildMemberUpdated(SocketGuildUser before, SocketGuildUser after) => 
            OnUserUpdated(before, after);

        public override Task OnUserUpdated(SocketUser before, SocketUser after)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (string.IsNullOrEmpty(before.Username) || string.IsNullOrEmpty(after.Username))
                        return;

                    if (before.Username == after.Username)
                        return;

                    var updated = new List<SocketGuild>();
                    foreach (var guild in SocketClient.Guilds.Select(x => x.GetUser(after.Id)?.Guild).Where(x => x != null))
                    {
                        try
                        {
                            var settings = await Settings.Read<LogSettings>(guild.Id, false);
                            if (settings == null)
                                continue;

                            var eventChannelId = settings.EventNameChangedChannel;
                            if (eventChannelId == 0)
                                continue;

                            var eventChannel = guild.TextChannels.FirstOrDefault(x => x.Id == eventChannelId);
                            if (eventChannel == null)
                                continue;

                            await Communicator.SendMessage(eventChannel, $"`{before.Username}` changed to `{after.Username}` (<@{after.Id}>)");
                            updated.Add(guild);
                        }
                        catch (Exception ex)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Error, "Log", $"Failed to process user {after.Id} update for guild {guild.Name} ({guild.Id})", ex));
                        }
                    }

                    if (updated.Any())
                        await Logger.Log(new LogMessage(LogSeverity.Info, "Log", $"Logged username change {before.Username} -> {after.Username} ({after.Id}) on {updated.Count} servers: {string.Join(", ", updated.Select(x => $"{x.Name} ({x.Id})"))}"));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Log", "Failed to process user update", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
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

                    var settings = await Settings.Read<LogSettings>(guild.Id, false).ConfigureAwait(false);
                    if (settings == null)
                        return;

                    var eventChannelId = settings.EventMessageDeletedChannel;
                    if (eventChannelId == 0)
                        return;

                    var eventChannel = guild.Channels.First(x => x.Id == eventChannelId) as ISocketMessageChannel;
                    if (eventChannel == null)
                        return;

                    if (settings.EventMessageDeletedChannelFilter.Contains(channel.Id))
                        return;

                    var filter = settings.EventMessageDeletedFilter;
                    try
                    {
                        if (!String.IsNullOrWhiteSpace(filter) && Regex.IsMatch(userMessage.Content, filter, RegexOptions.None, TimeSpan.FromSeconds(3)))
                            return;
                    }
                    catch (ArgumentException)
                    {
                        await Communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex is malformed.");
                        return;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        await Communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex takes too long to evaluate.");
                        return;
                    }

                    await LogSingleMessage(userMessage, guild, textChannel, eventChannel);
                }
                catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Log", "Missing permissions to log deleted message", ex));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Log", "Failed to process deleted message", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> cacheables, ISocketMessageChannel channel)
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

                    var settings = await Settings.Read<LogSettings>(guild.Id, false).ConfigureAwait(false);
                    if (settings == null)
                        return;

                    var eventChannelId = settings.EventMessageDeletedChannel;
                    if (eventChannelId == 0)
                        return;

                    var eventChannel = guild.Channels.First(x => x.Id == eventChannelId) as ISocketMessageChannel;
                    if (eventChannel == null)
                        return;

                    if (settings.EventMessageDeletedChannelFilter.Contains(channel.Id))
                        return;

                    var logs = new List<string>();
                    var filter = settings.EventMessageDeletedFilter;
                    try
                    {
                        foreach (var message in messages)
                        {
                            if (!String.IsNullOrWhiteSpace(filter) && Regex.IsMatch(message.Content, filter, RegexOptions.None, TimeSpan.FromSeconds(3)))
                                continue;

                            var sb = new StringBuilder();
                            sb.AppendLine($"**Message by {message.Author.Mention} in {textChannel.Mention} was deleted:**");

                            if (!string.IsNullOrWhiteSpace(message.Content))
                                sb.AppendLine(message.Content);

                            if (message.Attachments.Any())
                                sb.AppendLine(string.Join("\n", message.Attachments.Select(a => a.Url)));

                            sb.Append(message.Timestamp.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC"));

                            if (sb.Length > EmbedBuilder.MaxDescriptionLength / 2)
                            {
                                try
                                {
                                    await LogSingleMessage(message, guild, textChannel, eventChannel);
                                }
                                catch (Exception ex)
                                {
                                    await Logger.Log(new LogMessage(LogSeverity.Error, "Log", "Failed to log single deleted message in a bulk delete", ex));
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
                        await Communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex is malformed.");
                        await Logger.Log(new LogMessage(LogSeverity.Info, "Log", $"Didn't log deleted messages on {guild.Name} ({guild.Id}) because the message filter regex is malformed"));
                        return;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        await Communicator.SendMessage(eventChannel, "Failed to log a deleted message because your message filter regex takes too long to evaluate.");
                        await Logger.Log(new LogMessage(LogSeverity.Info, "Log", $"Didn't log deleted messages on {guild.Name} ({guild.Id}) because the message filter regex took to long to evaluate"));
                        return;
                    }

                    await Logger.Log(new LogMessage(LogSeverity.Verbose, "Log", $"Logging {logs.Count} deleted messages on {guild.Name} ({guild.Id})"));

                    var embed = new EmbedBuilder();
                    var delimiter = "\n\n";
                    foreach (var log in logs)
                    {
                        var totalLength = log.Length + delimiter.Length;
                        if (totalLength > EmbedBuilder.MaxDescriptionLength)
                            throw new InvalidOperationException(); // Shouldn't happen

                        if ((embed.Description?.Length ?? 0) + totalLength > EmbedBuilder.MaxDescriptionLength)
                        {
                            await eventChannel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
                            embed = new EmbedBuilder();
                        }
                        else
                        {
                            embed.Description += log + delimiter;
                        }
                    }

                    if (!string.IsNullOrEmpty(embed.Description))
                        await eventChannel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
                }
                catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Log", "Missing permissions to log deleted messages", ex));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Log", $"Failed to process deleted messages on {(channel as ITextChannel)?.GuildId}", ex));
                }
            });

            return Task.CompletedTask;
        }

        private async Task LogSingleMessage(IUserMessage userMessage, IGuild guild, ITextChannel textChannel, IMessageChannel eventChannel)
        {
            await Logger.Log(new LogMessage(LogSeverity.Verbose, "Log", $"Logging deleted message from {userMessage.Author.Username} on {guild.Name}"));
            var preface = $"**Message by {userMessage.Author.Mention} in {textChannel.Mention} was deleted:**\n";
            var embed = new EmbedBuilder()
                .WithDescription(preface + userMessage.Content.Truncate(EmbedBuilder.MaxDescriptionLength - preface.Length))
                .WithFooter(fb => fb.WithText($"{userMessage.Timestamp.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC")} (deleted on {DateTime.Now.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC")})"));

            if (userMessage.Attachments.Any())
                embed.AddField(efb => efb.WithName("Attachments").WithValue(string.Join(", ", userMessage.Attachments.Select(a => a.Url))).WithIsInline(false));

            await eventChannel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }
    }
}
