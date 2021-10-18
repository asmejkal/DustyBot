using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Utility;
using DustyBot.Helpers;
using DustyBot.Settings;

namespace DustyBot.Modules
{
    [Module("Notifications", "Notifies you when someone mentions a specific word.")]
    class NotificationsModule : Module
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

            public KeywordTree(IEnumerable<Notification> notifications, IEnumerable<ulong> ignoredUsers)
            {
                foreach (var n in notifications.Where(x => !ignoredUsers.Contains(x.User)))
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
        public const int DailyNotificationQuotaPerServer = 200;

        private static readonly TimeSpan NotificationTimeoutDelay = TimeSpan.FromSeconds(8);

        private ICommunicator Communicator { get; }
        private ISettingsService Settings { get; }
        private ILogger Logger { get; }
        private IUserFetcher UserFetcher { get; }

        private Dictionary<(ulong userId, ulong channelId), HashSet<ulong>> ActiveMessages { get; } = new Dictionary<(ulong userId, ulong channelId), HashSet<ulong>>();

        private ConcurrentDictionary<ulong, KeywordTree> KeywordTrees { get; } = new ConcurrentDictionary<ulong, KeywordTree>();

        public NotificationsModule(ICommunicator communicator, ISettingsService settings, ILogger logger, IUserFetcher userFetcher)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            UserFetcher = userFetcher;
        }

        [Command("notifications", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("notif", "help"), Alias("noti", "help"), Alias("notification", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: HelpBuilder.GetModuleHelpEmbed(this, command.Prefix));
        }

        [Command("notifications", "add", "Adds a word that you will be notified for when it is mentioned in this server.")]
        [Alias("notification", "add", true), Alias("notif", "add"), Alias("noti", "add")]
        [Alias("notifications", true), Alias("notification"), Alias("notif"), Alias("noti")]
        [Parameter("Word", ParameterType.String, ParameterFlags.Remainder, "when this word is mentioned in this server you will receive a notification")]
        public async Task AddNotification(ICommand command)
        {
            if (command["Word"].AsString.Length < MinNotificationLength)
            {
                await command.ReplyError(Communicator, $"A notification has to be at least {MinNotificationLength} characters long.");
                return;
            }

            if (command["Word"].AsString.Length > MaxNotificationLength)
            {
                await command.ReplyError(Communicator, $"A notification can't be longer than {MaxNotificationLength} characters.");
                return;
            }

            try
            {
                if ((await command.Guild.GetCurrentUserAsync()).GetPermissions((IGuildChannel)command.Channel).ManageMessages)
                    await command.Message.DeleteAsync();
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to delete notification message.", ex));
            }            

            await Settings.Modify(command.GuildId, (NotificationSettings s) =>
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

                KeywordTrees[command.GuildId] = new KeywordTree(s.Notifications, s.IgnoredUsers);
            });

            await command.ReplySuccess(Communicator, "You will now be notified when this word is mentioned (please make sure your privacy settings allow the bot to DM you).");
        }

        [Command("notifications", "remove", "Removes a notified word.")]
        [Alias("notification", "remove", true), Alias("notif", "remove"), Alias("noti", "remove")]
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
                await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to delete notification message.", ex));
            }

            var lowered = command["Word"].AsString.ToLowerInvariant();
            await Settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                if (s.Notifications.RemoveAll(x => x.User == command.Message.Author.Id && x.LoweredWord == lowered) < 1)
                    throw new CommandException($"You don't have this word set as a notification. You can see all your notifications with `notification list`.");

                KeywordTrees[command.GuildId] = new KeywordTree(s.Notifications, s.IgnoredUsers);
            });

            await command.ReplySuccess(Communicator, "You will no longer be notified when this word is mentioned.");
        }

        [Command("notifications", "clear", "Removes all notified words on this server.")]
        [Alias("notification", "clear", true), Alias("notif", "clear"), Alias("noti", "clear")]
        public async Task ClearNotifications(ICommand command)
        {
            await Settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                s.Notifications.RemoveAll(x => x.User == command.Message.Author.Id);
                KeywordTrees[command.GuildId] = new KeywordTree(s.Notifications, s.IgnoredUsers);
            });

            await command.ReplySuccess(Communicator, "You will no longer be notified on this server.");
        }

        [Command("notifications", "pause", "Disables all notifications on this server until you turn them back on.")]
        [Alias("notification", "pause", true), Alias("notif", "pause"), Alias("noti", "pause")]
        public async Task PauseNotifications(ICommand command)
        {
            await Settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                s.IgnoredUsers.Add(command.Author.Id);
                KeywordTrees[command.GuildId] = new KeywordTree(s.Notifications, s.IgnoredUsers);
            });

            await command.ReplySuccess(Communicator, "You will not be notified on this server until you use `notifications resume`.");
        }

        [Command("notifications", "resume", "Turns paused notifications back on.")]
        [Alias("notification", "resume", true), Alias("notif", "resume"), Alias("noti", "resume")]
        [Alias("notifications", "unpause"), Alias("notification", "unpause"), Alias("notif", "unpause"), Alias("noti", "unpause")]
        public async Task ResumeNotifications(ICommand command)
        {
            await Settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                s.IgnoredUsers.Remove(command.Author.Id);
                KeywordTrees[command.GuildId] = new KeywordTree(s.Notifications, s.IgnoredUsers);
            });

            await command.ReplySuccess(Communicator, "You will now be notified on this server again.");
        }

        [Command("notifications", "block", "Blocks a person from triggering your notifications.", CommandFlags.DirectMessageAllow)]
        [Alias("notification", "block", true), Alias("notif", "block"), Alias("noti", "block")]
        [Parameter("User", ParameterType.User, "a user ID or a mention")]
        [Comment("You will not get notifications for any messages from a blocked person. \n\nThis command can be used in a DM. See [this guide](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-) if you don't know how to find the user's ID.")]
        public async Task BlockNotifications(ICommand command)
        {
            try
            {
                if (command.Channel is IGuildChannel guildChannel &&
                    (await command.Guild.GetCurrentUserAsync()).GetPermissions(guildChannel).ManageMessages)
                {
                    await command.Message.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to delete notification message.", ex));
            }

            var user = await command["User"].AsUser;
            await Settings.ModifyUser(command.Author.Id, (UserNotificationSettings s) => s.BlockedUsers.Add(user.Id));

            var messages = await command.ReplySuccess(Communicator, "You will no longer receive notifications for any messages from this person.");
            
            if (command.Channel is IGuildChannel)
                messages.First().DeleteAfter(3);
        }

        [Command("notifications", "unblock", "Unblocks a person.", CommandFlags.DirectMessageAllow)]
        [Alias("notification", "unblock", true), Alias("notif", "unblock"), Alias("noti", "unblock")]
        [Parameter("User", ParameterType.User, "a user ID or mention")]
        [Comment("This command can be used in a DM. You will receive notifications for messages from this person again.")]
        public async Task UnblockNotifications(ICommand command)
        {
            try
            {
                if (command.Channel is IGuildChannel guildChannel &&
                    (await command.Guild.GetCurrentUserAsync()).GetPermissions(guildChannel).ManageMessages)
                {
                    await command.Message.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to delete notification message.", ex));
            }

            var user = await command["User"].AsUser;
            await Settings.ModifyUser(command.Author.Id, (UserNotificationSettings s) => s.BlockedUsers.Remove(user.Id));

            var messages = await command.ReplySuccess(Communicator, "You will receive notifications for messages from this person again.");

            if (command.Channel is IGuildChannel)
                messages.First().DeleteAfter(3);
        }

        [Command("notifications", "ignore", "channel", "Ignore messages in this channel for notifications. Use again to un-ignore.")]
        [Alias("notification", "ignore", "channel", true), Alias("notif", "ignore", "channel"), Alias("noti", "ignore", "channel")]
        public async Task IgnoreNotificationsChannel(ICommand command)
        {
            var removed = await Settings.Modify(command.GuildId, (NotificationSettings s) =>
            {
                if (!s.UserIgnoredChannels.TryGetValue(command.Author.Id, out var ignoredChannels))
                    s.UserIgnoredChannels.Add(command.Author.Id, ignoredChannels = new HashSet<ulong>());

                var remove = ignoredChannels.Contains(command.Channel.Id);
                if (remove)
                    ignoredChannels.Remove(command.Channel.Id);
                else
                    ignoredChannels.Add(command.Channel.Id);

                return remove;
            });

            if (removed)
                await command.ReplySuccess(Communicator, "You will now receive notifications from this channel again.");
            else
                await command.ReplySuccess(Communicator, "You will no longer receive notifications from this channel.");
        }

        [Command("notification", "ignore", "active", "channel", "Skip notifications from the channel you're currently active in.")]
        [Alias("notifications", "ignore", "active", "channel", true), Alias("notif", "ignore", "active", "channel"), Alias("noti", "ignore", "active", "channel")]
        [Comment("All notifications will be delayed by a small amount. If you start typing in the channel where someone triggered a notification, you won't be notified.\n\nUse this command again to disable.")]
        public async Task ToggleIgnoreActiveChannel(ICommand command)
        {
            var enabled = await Settings.ModifyUser(command.Author.Id, (UserNotificationSettings s) => s.IgnoreActiveChannel = !s.IgnoreActiveChannel);

            await command.ReplySuccess(Communicator, enabled ? "You won't be notified for messages in channels you're currently being active in. This causes a small delay for all notifications." : "You will now be notified for every message instantly.");
        }

        [Command("notification", "list", "Lists all your notified words on this server.")]
        [Alias("notifications", "list", true), Alias("notif", "list"), Alias("noti", "list")]
        [Comment("Sends a direct message.")]
        public async Task ListNotifications(ICommand command)
        {
            var settings = await Settings.Read<NotificationSettings>(command.GuildId);

            var userNotifs = settings.Notifications.Where(x => x.User == command.Message.Author.Id).ToList();
            if (userNotifs.Count <= 0)
            {
                await command.Reply(Communicator, "You don't have any notified words on this server. Use `notification add` to add some.");
                return;
            }

            var result = new StringBuilder();
            result.AppendLine($"Your notified words on `{command.Guild.Name}`:");
            foreach (var n in userNotifs)
                result.AppendLine($"`{n.OriginalWord}` – notified `{n.TriggerCount}` times");

            try
            {
                var dm = await command.Message.Author.GetOrCreateDMChannelAsync();
                await dm.SendMessageAsync(result.ToString());
            }
            catch (Discord.Net.HttpException)
            {
                await command.ReplyError(Communicator, $"Failed to send a direct message. Please check that your privacy settings allow the bot to DM you.");
                return;
            }

            await command.ReplySuccess(Communicator, "Please check your direct messages.");
        }

        private void RegisterPendingNotification((ulong, ulong) key, ulong message)
        {
            lock (ActiveMessages)
            {
                if (!ActiveMessages.TryGetValue(key, out var messages))
                    ActiveMessages.Add(key, messages = new HashSet<ulong>());

                messages.Add(message);
            }
        }

        private bool TryClaimPendingNotification((ulong, ulong) key, ulong message)
        {
            lock (ActiveMessages)
            {
                if (!ActiveMessages.TryGetValue(key, out var messages))
                    return false;

                messages.Remove(message);

                if (!messages.Any())
                    ActiveMessages.Remove(key);

                return true;
            }
        }

        private void ResetPendingNotifications(ulong user, ulong channel)
        {
            lock (ActiveMessages)
            {
                ActiveMessages.Remove((user, channel));
            }
        }

        public override async Task OnUserIsTyping(SocketUser user, ISocketMessageChannel channel)
        {
            try
            {
                ResetPendingNotifications(user.Id, channel.Id);
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to process typing event", ex));
            }
        }

        public override Task OnMessageReceived(SocketMessage message)
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

                    var settings = await Settings.Read<NotificationSettings>(channel.Guild.Id, false);
                    if (settings == null || settings.Notifications.Count <= 0)
                        return;

                    ResetPendingNotifications(user.Id, channel.Id);

                    var tree = KeywordTrees.GetOrAdd(channel.Guild.Id, x => new KeywordTree(settings.Notifications, settings.IgnoredUsers));
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

                        var targetUser = (IGuildUser)channel.Guild.GetUser(n.User) ?? await UserFetcher.FetchGuildUserAsync(channel.Guild.Id, n.User);
                        if (targetUser == null)
                            continue;

                        if (!targetUser.GetPermissions(channel).ViewChannel)
                            continue;

                        notifiedUsers.Add(n.User);

                        TaskHelper.FireForget(async () =>
                        {
                            try
                            {
                                // Check blocklist
                                var targetUserSettings = await Settings.ReadUser<UserNotificationSettings>(targetUser.Id, false);
                                if (targetUserSettings != null && targetUserSettings.BlockedUsers.Contains(user.Id))
                                {
                                    await Logger.Log(new LogMessage(LogSeverity.Info, "Notifications", $"Blocking a notification from user {user.Id} for {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) with trigger \"{n.OriginalWord}\" on {channel.Guild.Name} ({channel.Guild.Id})"));
                                    return;
                                }

                                bool quotaExceeded = false;
                                bool quotaThresholdReached = false;
                                var ignoredChannel = await Settings.Modify(channel.Guild.Id, (NotificationSettings s) =>
                                {
                                    if (s.UserIgnoredChannels.TryGetValue(targetUser.Id, out var ignoredChannels) && 
                                        ignoredChannels.Contains(channel.Id))
                                    {
                                        return true;
                                    }

                                    // Raise notification counter
                                    s.RaiseCount(n.User, n.LoweredWord);

                                    // Check and update daily quota
                                    var today = DateTime.UtcNow.Date;
                                    if (s.CurrentQuotaDate.Date != today)
                                    {
                                        s.UserQuotas.Clear();
                                        s.CurrentQuotaDate = today;
                                    }

                                    s.UserQuotas.TryGetValue(n.User, out var quota);
                                    s.UserQuotas[n.User] = ++quota;

                                    if (quota == DailyNotificationQuotaPerServer)
                                        quotaThresholdReached = true;
                                    else if (quota > DailyNotificationQuotaPerServer)
                                        quotaExceeded = true;

                                    return false;
                                });

                                if (ignoredChannel)
                                    return;

                                if (quotaExceeded)
                                    return;

                                if (targetUserSettings?.IgnoreActiveChannel ?? false)
                                {
                                    var messageKey = (targetUser.Id, channel.Id);
                                    RegisterPendingNotification(messageKey, message.Id);

                                    await Task.Delay(NotificationTimeoutDelay);

                                    if (!TryClaimPendingNotification(messageKey, message.Id))
                                    {
                                        await Logger.Log(new LogMessage(LogSeverity.Info, "Notifications", $"Notification canceled for user {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) with trigger \"{n.OriginalWord}\" on {channel.Guild.Name} ({channel.Guild.Id})"));
                                        return;
                                    }
                                }                                

                                var dm = await targetUser.GetOrCreateDMChannelAsync();
                                var footer = $"\n\n`{message.CreatedAt.ToUniversalTime().ToString("HH:mm UTC", CultureInfo.InvariantCulture)}` | [Show]({message.GetLink()}) | {channel.Mention}";

                                var embed = new EmbedBuilder()
                                    .WithAuthor(x => x.WithName(message.Author.Username).WithIconUrl(message.Author.GetAvatarUrl()))
                                    .WithDescription(message.Content.Truncate(EmbedBuilder.MaxDescriptionLength - footer.Length) + footer);

                                await dm.SendMessageAsync($"🔔 `{message.Author.Username}` mentioned `{n.OriginalWord}` on `{channel.Guild.Name}`:", embed: embed.Build());
                                await Logger.Log(new LogMessage(LogSeverity.Verbose, "Notifications", $"Notified user {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) with trigger \"{n.OriginalWord}\" on {channel.Guild.Name} ({channel.Guild.Id})"));

                                if (quotaThresholdReached)
                                {
                                    await Logger.Log(new LogMessage(LogSeverity.Info, "Notifications", $"Notification quota threshold reached for user {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) with trigger \"{n.OriginalWord}\" on {channel.Guild.Name} ({channel.Guild.Id})"));

                                    var now = DateTime.UtcNow;
                                    var timeLeft = now.Date.AddDays(1) - now;
                                    await dm.SendMessageAsync($"You've reached your daily notification limit on server `{channel.Guild.Name}`. Your quota will reset in `{timeLeft.SimpleFormat()}`. \nWe're sorry, but this is a necessary safeguard to prevent abuse.");
                                }
                            }
                            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50007)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Verbose, "Notifications", $"Blocked DMs from {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", $"Failed to process notification for user {targetUser.Username}#{targetUser.Discriminator} ({targetUser.Id}) on {channel.Guild.Name} ({channel.Guild.Id})", ex));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Notifications", "Failed to process message", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
