using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;
using LiteDB;
using DustyBot.Helpers;

namespace DustyBot.Settings
{
    public class ScheduleEvent : IComparable<ScheduleEvent>
    {
        public int Id { get; set; }
        public string Tag { get; set; }

        private DateTime _date;
        private DateTime _wholeDay;
        public DateTime Date {
            get => HasTime ? _date : _wholeDay;
            set
            {
                _date = value;
                _wholeDay = ConvertToWholeDay(value);
            }
        }

        public bool HasTime { get; set; }
        public string Description { get; set; }

        [BsonIgnore]
        public bool HasTag => Tag != null;

        public int CompareTo(ScheduleEvent other)
        {
            int c = 0;
            if ((c = Date.CompareTo(other.Date)) != 0)
                return c;

            if ((c = Description.CompareTo(other.Description)) != 0)
                return c;
            
            if ((c = (Tag ?? string.Empty).CompareTo(other.Tag ?? string.Empty)) != 0)
                return c;
            
            return Id.CompareTo(other.Id);
        }

        private static DateTime ConvertToWholeDay(DateTime dt) => dt.Date.Add(new TimeSpan(23, 59, 59));
    }
    
    public abstract class ScheduleCalendar
    {
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }

        public string Tag { get; set; }
        
        public string Title { get; set; }
        public string Footer { get; set; }

        [BsonIgnore]
        public bool HasTag => Tag != null;
    }

    public class RangeScheduleCalendar : ScheduleCalendar
    {
        //Inclusive
        private DateTime _beginDate;
        public DateTime BeginDate { get => _beginDate; set => _beginDate = value.Date; }

        //Exclusive
        private DateTime _endDate;
        public DateTime EndDate { get => _endDate; set => _endDate = value.Date; }

        [BsonIgnore]
        public bool HasEndDate => _endDate != DateTime.MaxValue.Date;

        [BsonIgnore]
        public bool IsMonthCalendar => EndDate - BeginDate == TimeSpan.FromDays(DateTime.DaysInMonth(BeginDate.Year, BeginDate.Month)) && BeginDate.Day == 1;
    }

    public abstract class UpcomingScheduleCalendar : ScheduleCalendar
    {
    }

    public class UpcomingSpanScheduleCalendar : UpcomingScheduleCalendar
    {
        public int DaysSpan { get; set; }
    }

    public class UpcomingWeekScheduleCalendar : UpcomingScheduleCalendar
    {
    }

    public enum EventFormat
    {
        Default,
        KoreanDate,
        MonthName
    }

    public class ScheduleSettings : BaseServerSettings
    {
        public int NextEventId { get; set; } = 1;

        public SortedList<ScheduleEvent> Events { get; set; } = new SortedList<ScheduleEvent>();
        public List<ScheduleCalendar> Calendars { get; set; } = new List<ScheduleCalendar>();

        public ulong ScheduleRole { get; set; }
        public EventFormat EventFormat { get; set; } = EventFormat.Default;
        public TimeSpan TimezoneOffset { get; set; } = TimeSpan.FromHours(9);
        public DateTime LastUpcomingCalendarsUpdate { get; set; } = DateTime.MinValue.Date;

        public bool ShowMigrateHelp { get; set; }
    }
}
