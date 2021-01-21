namespace DustyBot.Database.Mongo.Models
{
    public class NotificationSetting
    {
        public string Tag { get; set; }
        public ulong Channel { get; set; }
        public ulong Role { get; set; }
    }
}
