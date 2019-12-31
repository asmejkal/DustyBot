using Discord.WebSocket;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Helpers;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Discord;
using System.Threading;
using System.Collections.Concurrent;

namespace DustyBot.Services
{
    class ScheduleService : IScheduleService, IDisposable
    {
        private class NotificationContext : IDisposable
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

        public ISettingsProvider Settings { get; private set; }
        public DiscordSocketClient Client { get; private set; }
        public ILogger Logger { get; private set; }

        public static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(15); //Some timezones have quarter-hour fractions

        private bool Updating { get; set; }
        private object UpdatingLock = new object();

        private Timer UpdateTimer { get; set; }
        private ConcurrentDictionary<ulong, NotificationContext> Notifications { get; } = new ConcurrentDictionary<ulong, NotificationContext>();

        public ScheduleService(DiscordSocketClient client, ISettingsProvider settings, ILogger logger)
        {
            Settings = settings;
            Client = client;
            Logger = logger;
        }

        public async Task Start()
        {
            // Calendar updates
            var delay = UpdateFrequency.Minutes - (DateTime.UtcNow.Minute % UpdateFrequency.Minutes);
            UpdateTimer = new Timer(OnUpdate, null, delay * 60000, (int)UpdateFrequency.TotalMilliseconds);

            // Event notifications
            foreach (var settings in await Settings.Read<ScheduleSettings>())
                await RefreshNotifications(settings.ServerId, settings);
        }

        public void Stop()
        {
            UpdateTimer?.Dispose();
            UpdateTimer = null;
        }

        public async Task RefreshNotifications(ulong serverId, ScheduleSettings settings)
        {
            var now = DateTime.UtcNow.Add(settings.TimezoneOffset);
            var context = Notifications.GetOrAdd(serverId, x => new NotificationContext(serverId, OnNotify));
            using (await context.Lock.ClaimAsync())
            {
                var next = settings.Events.SkipWhile(x => x.Date < now).FirstOrDefault(x => x.Notify && x.HasTime);
                if (context.UtcDueTime.HasValue && (next == null || context.UtcDueTime <= next.Date.Subtract(settings.TimezoneOffset)))
                    return;

                if (next == null)
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
                lock (UpdatingLock)
                {
                    if (Updating)
                        return; // Skip if the previous update is still running

                    Updating = true;
                }

                try
                {
                    foreach (var settings in await Settings.Read<ScheduleSettings>())
                    {
                        if (settings.Calendars.OfType<UpcomingScheduleCalendar>().Count() <= 0)
                            continue;

                        var targetToday = DateTime.UtcNow.Add(settings.TimezoneOffset).Date;
                        if (targetToday <= settings.LastUpcomingCalendarsUpdate.Date)
                            continue; //Already updated today (in target timezone)

                        var guild = Client.GetGuild(settings.ServerId) as IGuild;
                        if (guild == null)
                            continue;

                        foreach (var calendar in settings.Calendars.OfType<UpcomingScheduleCalendar>())
                        {
                            try
                            {
                                var channel = await guild.GetTextChannelAsync(calendar.ChannelId).ConfigureAwait(false);
                                var message = channel != null ? (await channel.GetMessageAsync(calendar.MessageId).ConfigureAwait(false)) as IUserMessage : null;
                                if (message == null)
                                {
                                    await Settings.Modify(guild.Id, (ScheduleSettings s) => s.Calendars.RemoveAll(x => x.MessageId == calendar.MessageId));
                                    await Logger.Log(new LogMessage(LogSeverity.Warning, "Service", $"Removed deleted calendar {calendar.MessageId} on {guild.Name} ({guild.Id})"));
                                    continue;
                                }

                                var (text, embed) = Modules.ScheduleModule.BuildCalendarMessage(calendar, settings);
                                await message.ModifyAsync(x => { x.Content = text; x.Embed = embed; }).ConfigureAwait(false);

                                await Logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Updated calendar {calendar.MessageId} on {guild.Name} ({settings.ServerId})."));
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to update calendar {calendar.MessageId} on {guild.Name} ({settings.ServerId}).", ex));
                            }
                        }

                        await Settings.Modify(guild.Id, (ScheduleSettings s) => s.LastUpcomingCalendarsUpdate = targetToday);
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Service", "Failed to update calendars.", ex));
                }
                finally
                {
                    Updating = false;
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
                    if (!Notifications.TryGetValue(serverId, out var context))
                        return;

                    using (await context.Lock.ClaimAsync())
                    {
                        if (!context.ValidateCallback(state))
                            return; // Old timer (can happen due to timer race conditions)

                        var settings = await Settings.Read<ScheduleSettings>(serverId, false);
                        if (settings == null)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Settings for server {serverId} not found."));
                            return;
                        }

                        var guild = Client.GetGuild(serverId) as IGuild;
                        if (guild == null)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Server {serverId} not found."));
                            return;
                        }

                        var dueTime = context.UtcDueTime.Value.Add(settings.TimezoneOffset);
                        var events = settings.Events.Where(x => x.Date == dueTime && x.Notify);
                        foreach (var e in events)
                        {
                            foreach (var s in settings.Notifications.Where(x => e.FitsTag(x.Tag)))
                            {
                                try
                                {
                                    var channel = await guild.GetTextChannelAsync(s.Channel).ConfigureAwait(false);
                                    if (channel == null)
                                        continue;

                                    var role = s.Role != default ? guild.GetRole(s.Role) : null;
                                    var embed = new EmbedBuilder()
                                        .WithTitle("🔔 Schedule")
                                        .WithDescription($"{(e.HasLink ? DiscordHelpers.BuildMarkdownUri(e.Description, e.Link) : e.Description)} is now on!");

                                    await channel.SendMessageAsync(role != null ? $"{role.Mention} " : "", embed: embed.Build());
                                    await Logger.Log(new LogMessage(LogSeverity.Verbose, "Service", $"Notified event {e.Description} ({e.Id}) on {guild.Name} ({guild.Id})."));
                                }
                                catch (Exception ex)
                                {
                                    await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to notify event {e.Description} ({e.Id}) on {guild.Name} ({guild.Id}).", ex));
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
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to process event notifications.", ex));
                }
            });
        }

        #region IDisposable 

        private bool _disposed = false;
                
        public void Dispose()
        {
            Dispose(true);
        }
                
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UpdateTimer?.Dispose();
                    UpdateTimer = null;
                }
                
                _disposed = true;
            }
        }

        #endregion
    }

}
