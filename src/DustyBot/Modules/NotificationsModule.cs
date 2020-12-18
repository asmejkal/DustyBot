using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Exceptions;
using DustyBot.Settings;
using Discord.WebSocket;
using System.Collections.Concurrent;
using DustyBot.Helpers;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;

namespace DustyBot.Modules
{
    [Module("Notifications", "Notifies you when someone mentions a specific word.")]
    internal sealed class NotificationsModule : IDisposable
    {
        //TODO: Look into multi-keyword searching algos
        //current profiling results: 16x faster than naive search (700 keywords, 2000 message length)
        public class KeywordTree
        {
            private class Node
            {
                public Dictionary<char, Node> Children { get; } = new Dictionary<char, Node>();
                public List<Notification> Notifications { get; set; }
            }

            private Node Root { get; set; } = new Node();

            public KeywordTree(IEnumerable<Notification> notifications)
            {
                foreach (var n in notifications)
                {
                    Node current = Root;
                    foreach (var c in n.LoweredWord)
                        current = current.Children.GetOrCreate(c);

                    if (current.Notifications == null)
                        current.Notifications = new List<Notification>();

                    current.Notifications.Add(n);
                }
            }

            public List<(int position, Notification notification)> Match(string s)
            {
                var result = new List<(int, Notification)>();
                s = s.ToLowerInvariant();
                for (int i = 0; i < s.Length; ++i)
                {
                    Node current = Root;
                    for (int j = i; j < s.Length; ++j)
                    {
                        if (!current.Children.TryGetValue(s[j], out current))
                            break;

                        if (current.Notifications != null)
                            foreach (var n in current.Notifications)
                                result.Add((i, n));
                    }
                }

                return result;
            }
        }

        public const int MinNotificationLength = 2;
        public const int MaxNotificationLength = 50;
        public const int MaxNotificationsPerUser = 15;
        private static readonly TimeSpan NotificationTimeoutDelay = TimeSpan.FromSeconds(8);

        private readonly BaseSocketClient _client;
        private readonly ISettingsService _settings;
        private readonly ILogger _logger;
        private readonly IUserFetcher _userFetcher;
        private readonly IFrameworkReflector _frameworkReflector;

        private readonly Dictionary<(ulong userId, ulong channelId), HashSet<ulong>> _activeMessages = new Dictionary<(ulong userId, ulong channelId), HashSet<ulong>>();
        private readonly ConcurrentDictionary<ulong, KeywordTree> _keywordTrees = new ConcurrentDictionary<ulong, KeywordTree>();

        public NotificationsModule(BaseSocketClient client, ISettingsService settings, ILogger logger, IUserFetcher userFetcher, IFrameworkReflector frameworkReflector)
        {
            _client = client;
            _settings = settings;
            _logger = logger;
            _userFetcher = userFetcher;
            _frameworkReflector = frameworkReflector;

            _client.MessageReceived += HandleMessageReceived;
            _client.UserIsTyping += HandleUserIsTyping;
        }

        [Command("notification", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("notif", "help"), Alias("noti", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(HelpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("notification", "add", "Adds a word to be notified on when mentioned in this server.")]
        [Alias("notifications", "add", true), Alias("notif", "add"), Alias("noti", "add")]
        [Alias("notifications", true), Alias("notification"), Alias("notif"), Alias("noti")]
        [Parameter("Word", ParameterType.String, ParameterFlags.Remainder, "when this word is mentioned in this server you will receive a notification")]
        public async Task AddNotification(ICommand command)
        {
            if (command["Word"].AsString.Length < MinNotificationLength)
            {
                await command.ReplyError($"A notification has to be at least {MinNotificationLength} characters long.");
                return;
            }

            if (command["Word"].AsString.Length > MaxNotificationLength)
            {
                await command.ReplyError($"A notification can't be longer than {MaxNotificationLength} characters.");
                return;
            }

            try
            {
                if ((await command.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel)command.Channel).ManageMessages)
                    await command.Message.DeleteAsync();
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to delete notification message.", ex));
            }            

            await _settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                var currentNotifs = s.Notifications.Where(x => x.User == command.Message.Author.Id).ToList();
                if (currentNotifs.Any(x => x.LoweredWord == command["Word"].AsString.ToLowerInvariant()))
                    throw new CommandException($"You are already being notified for this word.");

                if (currentNotifs.Count >= MaxNotificationsPerUser)
                    throw new CommandException($"You cannot have more notifications on this server. You can clean up your old notifications with `notification remove`.");

                s.Notifications.Add(new Notification()
                {
                    User = command.Message.Author.Id,
                    LoweredWord = command["Word"].AsString.ToLowerInvariant(),
                    OriginalWord = command["Word"]
                });

                _keywordTrees[command.GuildId] = new KeywordTree(s.Notifications);
            });

            await command.ReplySuccess("You will now be notified when this word is mentioned (please make sure your privacy settings allow the bot to DM you).");
        }

        [Command("notification", "remove", "Removes a notified word.")]
        [Alias("notifications", "remove", true), Alias("notif", "remove"), Alias("noti", "remove")]
        [Parameter("Word", ParameterType.String, ParameterFlags.Remainder, "a word you don't want to be notified on anymore")]
        public async Task RemoveNotification(ICommand command)
        {
            try
            {
                if ((await command.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel)command.Channel).ManageMessages)
                    await command.Message.DeleteAsync();
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to delete notification message.", ex));
            }

            var lowered = command["Word"].AsString.ToLowerInvariant();
            await _settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                if (s.Notifications.RemoveAll(x => x.User == command.Message.Author.Id && x.LoweredWord == lowered) < 1)
                    throw new CommandException($"You don't have this word set as a notification. You can see all your notifications with `notification list`.");

                _keywordTrees[command.GuildId] = new KeywordTree(s.Notifications);
            });

            await command.ReplySuccess("You will no longer be notified when this word is mentioned.");
        }

        [Command("notification", "list", "Lists all your notified words on this server.")]
        [Alias("notifications", "list", true), Alias("notif", "list"), Alias("noti", "list")]
        [Comment("Sends a direct message.")]
        public async Task ListNotifications(ICommand command)
        {
            var settings = await _settings.Read<NotificationSettings>(command.GuildId);

            var userNotifs = settings.Notifications.Where(x => x.User == command.Message.Author.Id).ToList();
            if (userNotifs.Count <= 0)
            {
                await command.Reply("You don't have any notified words on this server. Use `notification add` to add some.");
                return;
            }

            var result = new StringBuilder();
            result.AppendLine($"Your notified words on `{command.Guild.Name}`:");
            foreach (var n in userNotifs)
                result.AppendLine($"`{n.OriginalWord}` – notified `{n.TriggerCount}` times");

            try
            {
                await command.Message.Author.SendMessageAsync(result.ToString());
            }
            catch (Discord.Net.HttpException)
            {
                await command.ReplyError($"Failed to send a direct message. Please check that your privacy settings allow the bot to DM you.");
                return;
            }

            await command.ReplySuccess("Please check your direct messages.");
        }

        [Command("notification", "ignore", "active", "channel", "Skip notifications from the channel you're currently active in.")]
        [Alias("notifications", "ignore", "active", "channel", true), Alias("notif", "ignore", "active", "channel"), Alias("noti", "ignore", "active", "channel")]
        [Comment("All notifications will be delayed by a small amount. If you start typing in the channel where someone triggered a notification, you won't be notified.\n\nUse this command again to disable.")]
        public async Task ToggleIgnoreActiveChannel(ICommand command)
        {
            var enabled = await _settings.ModifyUser(command.Author.Id, (UserNotificationSettings s) => s.IgnoreActiveChannel = !s.IgnoreActiveChannel);

            await command.ReplySuccess(enabled ? "You won't be notified for messages in channels you're currently being active in. This causes a small delay for all notifications." : "You will now be notified for every message instantly.");
        }

        private void RegisterPendingNotification((ulong, ulong) key, ulong message)
        {
            lock (_activeMessages)
            {
                if (!_activeMessages.TryGetValue(key, out var messages))
                    _activeMessages.Add(key, messages = new HashSet<ulong>());

                messages.Add(message);
            }
        }

        private bool TryClaimPendingNotification((ulong, ulong) key, ulong message)
        {
            lock (_activeMessages)
            {
                if (!_activeMessages.TryGetValue(key, out var messages))
                    return false;

                messages.Remove(message);

                if (!messages.Any())
                    _activeMessages.Remove(key);

                return true;
            }
        }

        private void ResetPendingNotifications(ulong user, ulong channel)
        {
            lock (_activeMessages)
            {
                _activeMessages.Remove((user, channel));
            }
        }

        private async Task HandleUserIsTyping(SocketUser user, ISocketMessageChannel channel)
        {
            try
            {
                ResetPendingNotifications(user.Id, channel.Id);
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to process typing event", ex));
            }
        }

        private Task HandleMessageReceived(SocketMessage message)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (!(message.Channel is SocketTextChannel channel))
                        return;

                    if (!(message.Author is IGuildUser user))
                        return;

                    if (user.IsBot)
                        return;

                    var settings = await _settings.Read<NotificationSettings>(channel.Guild.Id, false);
                    if (settings == null || settings.Notifications.Count <= 0)
                        return;

                    ResetPendingNotifications(user.Id, channel.Id);

                    var tree = _keywordTrees.GetOrAdd(channel.Guild.Id, x => new KeywordTree(settings.Notifications));
                    var notifiedUsers = new HashSet<ulong>();
                    foreach (var (p, n) in tree.Match(message.Content))
                    {
                        if (notifiedUsers.Contains(n.User))
                            continue; // Raise only one notification per message

                        if (p > 0 && char.IsLetter(message.Content[p - 1]))
                            continue; // Inside a word (prefixed) - skip

                        if (p + n.LoweredWord.Length < message.Content.Length && char.IsLetter(message.Content[p + n.LoweredWord.Length]))
                            continue; // Inside a word (suffixed) - skip

                        if (message.Author.Id == n.User)
                            continue; // Don't notify on self-sent messages

                        var targetUser = (IGuildUser)channel.Guild.GetUser(n.User) ?? await _userFetcher.FetchGuildUserAsync(channel.Guild.Id, n.User);
                        if (targetUser == null)
                            continue;

                        if (!targetUser.GetPermissions(channel).ViewChannel)
                            continue;

                        notifiedUsers.Add(n.User);

                        TaskHelper.FireForget(async () =>
                        {
                            try
                            {
                                await _settings.Modify(channel.Guild.Id, (NotificationSettings s) => s.RaiseCount(n.User, n.LoweredWord));

                                var targetUserSettings = await _settings.ReadUser<UserNotificationSettings>(targetUser.Id, false);
                                if (targetUserSettings?.IgnoreActiveChannel ?? false)
                                {
                                    var messageKey = (targetUser.Id, channel.Id);
                                    RegisterPendingNotification(messageKey, message.Id);

                                    await Task.Delay(NotificationTimeoutDelay);

                                    if (!TryClaimPendingNotification(messageKey, message.Id))
                                    {
                                        await _logger.Log(new LogMessage(LogSeverity.Verbose, "Notifications", $"Notification canceled for user {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) and message {message.Id} on {channel.Guild.Name} ({channel.Guild.Id}) in #{channel.Name} ({channel.Id})"));
                                        return;
                                    }
                                }                                

                                var footer = $"\n\n`{message.CreatedAt.ToUniversalTime().ToString("HH:mm UTC", CultureInfo.InvariantCulture)}` | [Show]({message.GetLink()}) | {channel.Mention}";

                                var embed = new EmbedBuilder()
                                    .WithAuthor(x => x.WithName(message.Author.Username).WithIconUrl(message.Author.GetAvatarUrl()))
                                    .WithDescription(message.Content.Truncate(EmbedBuilder.MaxDescriptionLength - footer.Length) + footer);

                                await targetUser.SendMessageAsync($"🔔 `{message.Author.Username}` mentioned `{n.OriginalWord}` on `{channel.Guild.Name}`:", embed: embed.Build());
                            }
                            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50007)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Verbose, "Notifications", $"Blocked DMs from {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                            }
                            catch (Exception ex)
                            {
                                await _logger.Log(new LogMessage(LogSeverity.Error, "Notifications", $"Failed to process notification for user {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) on {channel.Guild.Name} ({channel.Guild.Id})", ex));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    await _logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to process message", ex));
                }
            });

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _client.MessageReceived -= HandleMessageReceived;
            _client.UserIsTyping -= HandleUserIsTyping;
        }
    }
}
