using Discord.WebSocket;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DustyBot.Settings.LiteDB;
using DustyBot.Helpers;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Discord;

namespace DustyBot.Services
{
    class ScheduleService : IDisposable, Framework.Services.IService
    {
        System.Threading.Timer _timer;

        public ISettingsProvider Settings { get; private set; }
        public DiscordSocketClient Client { get; private set; }
        public ILogger Logger { get; private set; }

        public static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(3); //Some timezones have quarter-hour fractions
        
        public ScheduleService(DiscordSocketClient client, ISettingsProvider settings, ILogger logger)
        {
            Settings = settings;
            Client = client;
            Logger = logger;
        }

        public void Start()
        {
            var delay = UpdateFrequency.Minutes - (DateTime.UtcNow.Minute % UpdateFrequency.Minutes);
            _timer = new System.Threading.Timer(OnUpdate, null, delay * 60000, (int)UpdateFrequency.TotalMilliseconds);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        void OnUpdate(object state)
        {
            TaskHelper.FireForget(async () =>
            {
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
            });            
        }
        
        #region IDisposable 

        private bool _disposed = false;
                
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
                
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                    _timer = null;
                }
                
                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }

}
