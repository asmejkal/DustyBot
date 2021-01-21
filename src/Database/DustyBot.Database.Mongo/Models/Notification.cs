namespace DustyBot.Database.Mongo.Models
{
    public class Notification
    {
        public string LoweredWord { get; set; }
        public string OriginalWord { get; set; }
        public ulong User { get; set; }
        public uint TriggerCount { get; set; }
    }
}
