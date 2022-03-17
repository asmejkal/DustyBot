using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Parsing;
using DustyBot.Database.Mongo.Collections.Notifications;
using DustyBot.Database.Mongo.Collections.Notifications.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Client;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using Microsoft.Extensions.Logging;
using NReco.Text;

namespace DustyBot.Service.Services.Notifications
{
    internal class NotificationsService : DustyBotService, INotificationsService
    {
        public const int MinNotificationLength = 2;
        public const int MaxNotificationLength = 50;
        public const int MaxNotificationsPerUser = 20;
        public const int DailyNotificationQuotaPerServer = 200;
        public static readonly TimeSpan ActivityDetectionDelay = TimeSpan.FromSeconds(8);

        private static readonly IReadOnlyCollection<Regex> IgnoredKeywordRegexes = new[]
        {
            new Regex(@"https?://", RegexOptions.Compiled),
            new Regex(@"<a?:\S+:\d+>", RegexOptions.Compiled)
        };

        private readonly ISettingsService _settings;
        private readonly INotificationSettingsService _userSettings;
        private readonly INotificationsSender _sender;
        private readonly IChannelActivityWatcher _channelActivityWatcher;

        private readonly Dictionary<ulong, AhoCorasickDoubleArrayTrie<Notification>?> _keywordTries = new();

        public NotificationsService(
            ISettingsService settings, 
            INotificationSettingsService userSettings, 
            INotificationsSender sender,
            IChannelActivityWatcher channelActivityWatcher)
            : base()
        {
            _settings = settings;
            _userSettings = userSettings;
            _sender = sender;
            _channelActivityWatcher = channelActivityWatcher;
        }

        public Task<AddKeywordsResult> AddKeywordsAsync(Snowflake guildId, Snowflake userId, IEnumerable<string> keywords, CancellationToken ct)
        {
            return _settings.Modify(guildId, (NotificationSettings s) =>
            {
                var userNotifications = s.Notifications.Where(x => x.User == userId).ToList();
                var newNotifications = new List<Notification>();
                foreach (var (keyword, i) in keywords.Select((x, i) => (x, i)))
                {
                    if (keyword.Length < MinNotificationLength)
                        return AddKeywordsResult.KeywordTooShort;

                    if (keyword.Length > MaxNotificationLength)
                        return AddKeywordsResult.KeywordTooLong;

                    if (userNotifications.Any(x => string.Compare(x.Keyword, keyword, true) == 0))
                        return AddKeywordsResult.DuplicateKeyword;

                    newNotifications.Add(new Notification(keyword, userId));
                }

                if (userNotifications.Count + newNotifications.Count >= MaxNotificationsPerUser)
                    return AddKeywordsResult.TooManyKeywords;

                s.Notifications.AddRange(newNotifications);
                RefreshTrie(s);
                return AddKeywordsResult.Success;
            }, ct);
        }

        public Task<RemoveKeywordResult> RemoveKeywordAsync(Snowflake guildId, Snowflake userId, string keyword, CancellationToken ct)
        {
            return _settings.Modify(guildId, (NotificationSettings s) =>
            {
                if (s.Notifications.RemoveAll(x => x.User == userId && string.Compare(x.Keyword, keyword, true) == 0) < 1)
                    return RemoveKeywordResult.NotFound;

                RefreshTrie(s);
                return RemoveKeywordResult.Success;
            }, ct);
        }

        public Task ClearKeywordsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (NotificationSettings s) =>
            {
                s.Notifications.RemoveAll(x => x.User == userId);
                RefreshTrie(s);
            }, ct);
        }

        public Task PauseNotificationsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (NotificationSettings s) =>
            {
                s.IgnoredUsers.Add(userId);
                RefreshTrie(s);
            }, ct);
        }

        public Task ResumeNotificationsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (NotificationSettings s) =>
            {
                s.IgnoredUsers.Remove(userId);
                RefreshTrie(s);
            }, ct);
        }

        public Task BlockUserAsync(Snowflake userId, Snowflake targetUserId, CancellationToken ct) =>
            _userSettings.BlockUserAsync(userId, targetUserId, ct);

        public Task UnblockUserAsync(Snowflake userId, Snowflake targetUserId, CancellationToken ct) =>
            _userSettings.UnblockUserAsync(userId, targetUserId, ct);

        public Task<bool> ToggleIgnoredChannelAsync(Snowflake guildId, Snowflake userId, Snowflake channelId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (NotificationSettings s) =>
            {
                if (!s.UserIgnoredChannels.TryGetValue(userId, out var ignoredChannels))
                    s.UserIgnoredChannels.Add(userId, ignoredChannels = new());

                if (ignoredChannels.Contains(channelId))
                {
                    ignoredChannels.Remove(channelId);
                    return false;
                }
                else
                {
                    ignoredChannels.Add(channelId);
                    return true;
                }
            }, ct);
        }

        public Task<bool> ToggleActivityDetectionAsync(Snowflake userId, CancellationToken ct) =>
            _userSettings.ToggleActivityDetectionAsync(userId, ct);

        public Task<bool> ToggleOptOutAsync(Snowflake userId, CancellationToken ct) =>
            _userSettings.ToggleOptOutAsync(userId, ct);

        public async Task<IEnumerable<Notification>> GetKeywordsAsync(Snowflake guildId, Snowflake userId, CancellationToken ct)
        {
            var settings = await _settings.Read<NotificationSettings>(guildId, false, ct);
            return settings?.Notifications.Where(x => x.User == userId) ?? Enumerable.Empty<Notification>();
        }

        protected override async ValueTask OnMessageReceived(BotMessageReceivedEventArgs e)
        {
            try
            {
                if (e.GuildId == null)
                    return;

                if (e.Message is not IGatewayUserMessage message || message.Author.IsBot)
                    return;

                var trie = await GetOrCreateTrieAsync(e.GuildId.Value);
                if (trie == null)
                    return;

                var guild = Bot.GetGuildOrThrow(e.GuildId.Value);
                var content = message.Content;

                var matches = trie.ParseText(content);
                var pendingNotifications = new Dictionary<ulong, Task>();
                foreach (var match in matches)
                {
                    if (pendingNotifications.ContainsKey(match.Value.User))
                        continue; // Raise only one notification per message

                    if (message.Author.Id == match.Value.User)
                        continue; // Don't notify on self-sent messages

                    if (match.Begin > 0 && char.IsLetter(content[match.Begin - 1]))
                        continue; // Inside a word (prefixed) - skip

                    if (match.End < content.Length && char.IsLetter(content[match.End]))
                        continue; // Inside a word (suffixed) - skip

                    if (IgnoredKeywordRegexes.Any(x => x.IsMatch(content.GetEnclosingWord(match.Begin, match.End))))
                        continue;

                    if ((await Bot.FindCommandsAsync(message)).Any(x => x.Command.HideInvocation()))
                        return; // Prevent snooping on commands

                    pendingNotifications.Add(match.Value.User, Task.Run(async () =>
                    {
                        try
                        {
                            if (e.Channel.Type == ChannelType.PrivateThread)
                                return; // Not supported, checking private thread membership is either too expensive or complex

                            var targetUser = await guild.GetOrFetchMemberAsync(match.Value.User, cancellationToken: Bot.StoppingToken);
                            if (targetUser == null)
                                return;

                            var permissions = guild.GetPermissions(e.Channel, targetUser);
                            if (!permissions.ViewChannels || !permissions.ReadMessageHistory)
                                return;

                            await SendNotificationAsync(match.Value, message, targetUser, guild, e.Channel, Bot.StoppingToken);
                        }
                        catch (OperationCanceledException ex)
                        {
                            Logger.WithArgs(e).WithUser(match.Value.User).LogWarning(ex, "Notification processing canceled");
                        }
                        catch (Exception ex)
                        {
                            Logger.WithArgs(e).WithUser(match.Value.User).LogError(ex, "Failed to send notification");
                        }
                    }));
                }

                await Task.WhenAll(pendingNotifications.Values);
            }
            catch (Exception ex)
            {
                Logger.WithArgs(e).LogError(ex, "Failed to process notification");
            }
        }

        private async Task SendNotificationAsync(
            Notification notification,
            IGatewayUserMessage message,
            IMember targetUser,
            IGatewayGuild guild,
            IMessageGuildChannel sourceChannel,
            CancellationToken ct)
        {
            try
            {
                using var scope = Logger.WithGuild(guild).WithMessage(message).WithMember(targetUser).BeginScope();

                // Check opt-out settings
                if (await _userSettings.HasOptedOutAsync(message.Author.Id, ct))
                    return;

                if (await _userSettings.HasOptedOutAsync(targetUser.Id, ct))
                    return;

                // Check blocklist
                var blockedUsers = await _userSettings.GetBlockedUsersAsync(targetUser.Id, ct);
                if (blockedUsers.Contains(message.Author.Id))
                {
                    Logger.LogInformation("Blocking a notification from user {SourceUserId}", message.Author.Id);
                    return;
                }

                bool quotaThresholdReached = false;
                var proceed = await _settings.Modify(guild.Id, (NotificationSettings s) =>
                {
                    if (s.UserIgnoredChannels.TryGetValue(targetUser.Id, out var ignoredChannels) &&
                        ignoredChannels.Contains(sourceChannel.Id))
                    {
                        return false;
                    }

                    // Raise notification counter
                    s.Notifications.First(x => x == notification).TriggerCount++;

                    // Check and update daily quota
                    var today = DateTime.UtcNow.Date;
                    if (s.CurrentQuotaDate.Date != today)
                    {
                        s.UserQuotas.Clear();
                        s.CurrentQuotaDate = today;
                    }

                    s.UserQuotas.TryGetValue(targetUser.Id, out var quota);
                    s.UserQuotas[targetUser.Id] = ++quota;

                    if (quota > DailyNotificationQuotaPerServer)
                        return false;
                    else if (quota == DailyNotificationQuotaPerServer)
                        quotaThresholdReached = true;

                    return true;
                }, ct);

                if (!proceed)
                    return;

                if (await _userSettings.IsActivityDetectionEnabledAsync(targetUser.Id, ct))
                {
                    if (await _channelActivityWatcher.WaitForUserActivityAsync(targetUser.Id, sourceChannel.Id, ActivityDetectionDelay, ct))
                    {
                        Logger.LogInformation("Notification canceled");
                        return;
                    }
                }

                await _sender.SendNotificationAsync(targetUser, message, notification, guild, sourceChannel, ct);
                Logger.LogTrace("Notified user with trigger {NotificationTrigger}", notification.Keyword);

                if (quotaThresholdReached)
                {
                    Logger.LogInformation("Notification quota threshold reached");

                    var now = DateTime.UtcNow;
                    await _sender.SendQuotaReachedWarningAsync(targetUser, guild, now.Date.AddDays(1) - now, ct);
                }
            }
            catch (RestApiException ex) when (ex.IsError(RestApiErrorCode.CannotSendMessagesToThisUser))
            {
                Logger.WithGuild(guild).WithMessage(message).WithUser(notification.User)
                    .LogInformation("Couldn't send notification due to blocked DMs by the user");
            }
        }

        private void RefreshTrie(NotificationSettings settings)
        {
            lock (_keywordTries)
            {
                _keywordTries[settings.ServerId] = CreateTrie(settings);
            }
        }

        private async Task<AhoCorasickDoubleArrayTrie<Notification>?> GetOrCreateTrieAsync(Snowflake guildId)
        {
            lock (_keywordTries)
            {
                if (_keywordTries.TryGetValue(guildId, out var result))
                    return result;
            }

            var settings = await _settings.Read<NotificationSettings>(guildId, false, Bot.StoppingToken);
            lock (_keywordTries)
            {
                if (_keywordTries.TryGetValue(guildId, out var result))
                    return result;

                _keywordTries.Add(guildId, result = CreateTrie(settings));
                return result;
            }
        }

        private static AhoCorasickDoubleArrayTrie<Notification>? CreateTrie(NotificationSettings settings)
        {
            if (settings == null)
                return null;

            var keywords = settings.Notifications
                .Where(x => !settings.IgnoredUsers.Contains(x.User))
                .Select(x => KeyValuePair.Create(x.Keyword, x));

            return keywords.Any() ? new AhoCorasickDoubleArrayTrie<Notification>(keywords, ignoreCase: true) : null;
        }
    }
}
