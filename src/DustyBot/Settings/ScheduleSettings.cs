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

        public DateTime Date { get; set; }
        public bool HasTime { get; set; }
        public string Description { get; set; }

        public int CompareTo(ScheduleEvent other)
        {
            var c = Date.CompareTo(other.Date);
            return c != 0 ? c : Description.CompareTo(other.Description);
        }
    }

    public class ScheduleData
    {
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }

        public string Header { get; set; }
        public string Footer { get; set; }

        public string GCalendarId { get; set; }

        public int NextEventId { get; set; } = 1;
        public SortedList<ScheduleEvent> Events { get; set; } = new SortedList<ScheduleEvent>();
    }

    public class ScheduleSettings : BaseServerSettings
    {
        public List<ScheduleData> ScheduleData { get; set; } = new List<ScheduleData>();
        public ulong ScheduleRole { get; set; }
    }
}
