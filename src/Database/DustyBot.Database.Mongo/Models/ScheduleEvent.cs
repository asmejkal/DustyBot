using System;
using DustyBot.Database.Mongo.Collections;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Models
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
        public string Link { get; set; }
        public bool Notify { get; set; }

        [BsonIgnore]
        public bool HasTag => Tag != null;

        [BsonIgnore]
        public bool HasLink => !string.IsNullOrEmpty(Link);

        public int CompareTo(ScheduleEvent other)
        {
            int c = 0;
            if ((c = Date.CompareTo(other.Date)) != 0)
                return c;

            if ((c = Description.CompareTo(other.Description)) != 0)
                return c;
            
            if ((c = (Tag ?? string.Empty).CompareTo(other.Tag ?? string.Empty)) != 0)
                return c;

            if ((c = (Link ?? string.Empty).CompareTo(other.Link ?? string.Empty)) != 0)
                return c;

            return Id.CompareTo(other.Id);
        }

        public bool FitsTag(string tag) => ScheduleSettings.CompareTag(tag, Tag);

        private static DateTime ConvertToWholeDay(DateTime dt) => dt.Date.Add(new TimeSpan(23, 59, 59));
    }
}
