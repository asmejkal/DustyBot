using System;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Models
{
    public class RangeScheduleCalendar : ScheduleCalendar
    {
        // Inclusive
        private DateTime _beginDate;
        public DateTime BeginDate { get => _beginDate; set => _beginDate = value.Date; }

        // Exclusive
        private DateTime _endDate;
        public DateTime EndDate { get => _endDate; set => _endDate = value.Date; }

        [BsonIgnore]
        public bool HasEndDate => _endDate != DateTime.MaxValue.Date;

        [BsonIgnore]
        public bool IsMonthCalendar => EndDate - BeginDate == TimeSpan.FromDays(DateTime.DaysInMonth(BeginDate.Year, BeginDate.Month)) && BeginDate.Day == 1;

        public bool FitsDate(DateTime date) => date >= BeginDate && date < EndDate;
    }
}
