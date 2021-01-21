using DustyBot.Database.Mongo.Collections;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Models
{
    [BsonKnownTypes(typeof(RangeScheduleCalendar), typeof(UpcomingScheduleCalendar))]
    public abstract class ScheduleCalendar
    {
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }

        public string Tag { get; set; }
        
        public string Title { get; set; }
        public string Footer { get; set; }

        [BsonIgnore]
        public bool HasTag => Tag != null;

        [BsonIgnore]
        public bool HasAllTag => ScheduleSettings.IsAllTag(Tag);

        public bool FitsTag(string tag) => ScheduleSettings.CompareTag(Tag, tag);
    }
}
