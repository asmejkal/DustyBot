using System;
using System.Collections.Generic;
using DustyBot.Core.Collections;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class ScheduleSettings : BaseServerSettings
    {
        public const string AllTag = "all";

        public const int DefaultUpcomingEventsDisplayLimit = 15;
        public int NextEventId { get; set; } = 1;

        public SortedList<ScheduleEvent> Events { get; set; } = new SortedList<ScheduleEvent>();
        public List<ScheduleCalendar> Calendars { get; set; } = new List<ScheduleCalendar>();

        public ulong ScheduleRole { get; set; }
        public List<NotificationSetting> Notifications { get; set; } = new List<NotificationSetting>();
        public EventFormat EventFormat { get; set; } = EventFormat.MonthName;
        public TimeSpan TimezoneOffset { get; set; } = TimeSpan.FromHours(9);
        public string TimezoneName { get; set; }
        public int UpcomingEventsDisplayLimit { get; set; } = DefaultUpcomingEventsDisplayLimit;
        public DateTime LastUpcomingCalendarsUpdate { get; set; } = DateTime.MinValue.Date;

        public bool ShowMigrateHelp { get; set; }

        public static bool IsDefaultTag(string tag) => tag == null;

        public static bool IsAllTag(string tag) => string.Compare(tag, AllTag, true) == 0;

        public static bool CompareTag(string calendarTag, string tag, bool ignoreAllTag = false)
        {
            if (string.Compare(calendarTag, AllTag, true) == 0 && !ignoreAllTag)
                return true;

            return string.Compare(calendarTag, tag, true) == 0;
        }
    }
}
