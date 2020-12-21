using Discord.WebSocket;
using DustyBot.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Discord;
using System.Threading;
using System.Collections.Concurrent;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DustyBot.Services
{
    internal sealed class ScheduleService : IHostedService, IScheduleService, IDisposable
    {
        private sealed class NotificationContext : IDisposable
        {
            public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);
            public ulong ServerId { get; }
            public DateTime? UtcDueTime { get; set; }

            private Timer Timer { get; set; }
            private TimerCallback Callback { get; set; }
            private int Counter { get; set; }

            public NotificationContext(ulong serverId, TimerCallback callback)
            {
                ServerId = serverId;
                Callback = callback;
            }

            public void Replan(TimeSpan delay, DateTime utcDueTime)
            {
                Timer?.Dispose();
                Counter++;
                UtcDueTime = utcDueTime;
                Timer = new Timer(Callback, (ServerId, Counter), delay, new TimeSpan(Timeout.Infinite));
            }

            public void Disable()
            {
                Timer?.Dispose();
                Counter++;
                UtcDueTime = null;
                Timer = null;
            }

            public bool ValidateCallback(object state)
            {
                var (serverId, counter) = ((ulong, int))state;
                return Counter == counter && ServerId == serverId;
            }

            public void Dispose()
            {
                Timer?.Dispose();
                ((IDisposable)Lock).Dispose();
            }
        }

        private static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(15); //Some timezones have quarter-hour fractions

        private readonly BaseSocketClient _client;
        private readonly ISettingsService _settings;
        private readonly ILogger<ScheduleService> _logger;

        private readonly object _updatingLock = new object();
        private bool _updating;
        private Timer _updateTimer;
        private readonly ConcurrentDictionary<ulong, NotificationContext> _notifications = new ConcurrentDictionary<ulong, NotificationContext>();

        public ScheduleService(BaseSocketClient client, ISettingsService settings, ILogger<ScheduleService> logger)
        {
            _client = client;
            _settings = settings;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            // Calendar updates
            var delay = UpdateFrequency.Minutes - (DateTime.UtcNow.Minute % UpdateFrequency.Minutes);
            _updateTimer = new Timer(OnUpdate, null, delay * 60000, (int)UpdateFrequency.TotalMilliseconds);

            // Event notifications
            foreach (var settings in await _settings.Read<ScheduleSettings>())
                await RefreshNotifications(settings.ServerId, settings);
        }

        public Task StopAsync(CancellationToken ct)
        {
            _updateTimer?.Dispose();
            _updateTimer = null;

            foreach (var notification in _notifications.Values)
                notification.Disable();

            return Task.CompletedTask;
        }

        public async Task RefreshNotifications(ulong serverId, ScheduleSettings settings)
        {
            var now = DateTime.UtcNow.Add(settings.TimezoneOffset);
            var context = _notifications.GetOrAdd(serverId, x => new NotificationContext(serverId, OnNotify));
            using (await context.Lock.ClaimAsync())
            {
                var next = settings.Events.SkipWhile(x => x.Date < now).FirstOrDefault(x => x.Notify && x.HasTime);
                if (context.UtcDueTime.HasValue && (next == null || context.UtcDueTime <= next.Date.Subtract(settings.TimezoneOffset)))
                    return;

                if (next == null || (next.Date - now).TotalMilliseconds >= int.MaxValue)
                {
                    context.Disable();
                    return;
                }

                context.Replan(next.Date - now, next.Date.Subtract(settings.TimezoneOffset));
            }
        }

        private void OnUpdate(object state)
        {
            TaskHelper.FireForget(async () =>
            {
                lock (_updatingLock)
                {
                    if (_updating)
                        return; // Skip if the previous update is still running

                    _updating = true;
                }

                try
                {
                    foreach (var settings in await _settings.Read<ScheduleSettings>())
                    {
                        if (settings.Calendars.OfType<UpcomingScheduleCalendar>().Count() <= 0)
                            continue;

                        var targetToday = DateTime.UtcNow.Add(settings.TimezoneOffset).Date;
                        if (targetToday <= settings.LastUpcomingCalendarsUpdate.Date)
                            continue; //Already updated today (in target timezone)

                        var guild = _client.GetGuild(settings.ServerId) as IGuild;
                        if (guild == null)
                            continue;

                        var logger = _logger.WithScope(guild);
                        foreach (var calendar in settings.Calendars.OfType<UpcomingScheduleCalendar>())
                        {
                            try
                            {
                                var channel = await guild.GetTextChannelAsync(calendar.ChannelId);
                                var message = channel != null ? (await channel.GetMessageAsync(calendar.MessageId)) as IUserMessage : null;
                                if (message == null)
                                {
                                    await _settings.Modify(guild.Id, (ScheduleSettings s) => s.Calendars.RemoveAll(x => x.MessageId == calendar.MessageId));
                                    logger.LogInformation("Removed deleted calendar {CalendarMessageId}", calendar.MessageId);
                                    continue;
                                }

                                var (text, embed) = Modules.ScheduleModule.BuildCalendarMessage(calendar, settings);
                                await message.ModifyAsync(x => { x.Content = text; x.Embed = embed; });

                                logger.LogInformation("Updated calendar {CalendarMessageId}", calendar.MessageId);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to update calendar {CalendarMessageId}", calendar.MessageId);
                            }
                        }

                        await _settings.Modify(guild.Id, (ScheduleSettings s) => s.LastUpcomingCalendarsUpdate = targetToday);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update calendars");
                }
                finally
                {
                    _updating = false;
                }
            });
        }

        private void OnNotify(object state)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var (serverId, _) = ((ulong, int))state;
                    if (!_notifications.TryGetValue(serverId, out var context))
                        return;

                    using (await context.Lock.ClaimAsync())
                    {
                        if (!context.ValidateCallback(state))
                            return; // Old timer (can happen due to timer race conditions)

                        var guild = _client.GetGuild(serverId) as IGuild;
                        if (guild == null)
                        {
                            _logger.LogInformation("Server {" + LogFields.GuildId + "} not found", serverId);
                            return;
                        }

                        var logger = _logger.WithScope(guild);
                        var settings = await _settings.Read<ScheduleSettings>(serverId, false);
                        if (settings == null)
                        {
                            logger.LogWarning("Settings for server not found");
                            return;
                        }

                        var dueTime = context.UtcDueTime.Value.Add(settings.TimezoneOffset);
                        var events = settings.Events.Where(x => x.Date == dueTime && x.HasTime && x.Notify);
                        foreach (var e in events)
                        {
                            foreach (var s in settings.Notifications.Where(x => e.FitsTag(x.Tag)))
                            {
                                try
                                {
                                    var channel = await guild.GetTextChannelAsync(s.Channel);
                                    if (channel == null)
                                        continue;

                                    var role = s.Role != default ? guild.GetRole(s.Role) : null;
                                    var embed = new EmbedBuilder()
                                        .WithTitle("🔔 Schedule")
                                        .WithDescription($"{(e.HasLink ? DiscordHelpers.BuildMarkdownUri(e.Description, e.Link) : e.Description)} is now on!");

                                    await channel.SendMessageAsync(role != null ? $"{role.Mention} " : "", embed: embed.Build());
                                    logger.LogInformation("Notified event {ScheduleEventDescription} ({ScheduleEventDate}, TZ: {ScheduleTimezone}, ID: {ScheduleEventId})", e.Description, e.Date, settings.TimezoneOffset, e.Id);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Failed to notify event {ScheduleEventDescription} ({ScheduleEventId})", e.Description, e.Id);
                                }
                            }
                        }

                        var now = DateTime.UtcNow.Add(settings.TimezoneOffset);
                        var next = settings.Events.SkipWhile(x => x.Date <= dueTime).FirstOrDefault(x => x.Notify && x.HasTime);
                        if (next == default)
                        {
                            context.Disable();
                            return;
                        }

                        context.Replan(next.Date - now, next.Date.Subtract(settings.TimezoneOffset));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process event notifications");
                }
            });
        }
       
        public void Dispose()
        {
            _updateTimer?.Dispose();
            _updateTimer = null;

            foreach (var notification in _notifications.Values)
                notification.Dispose();
        }
    }
}
