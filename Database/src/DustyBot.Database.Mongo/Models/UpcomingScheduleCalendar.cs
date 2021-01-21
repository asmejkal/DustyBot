using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Models
{
    [BsonKnownTypes(typeof(UpcomingSpanScheduleCalendar), typeof(UpcomingWeekScheduleCalendar))]
    public abstract class UpcomingScheduleCalendar : ScheduleCalendar
    {
    }
}
