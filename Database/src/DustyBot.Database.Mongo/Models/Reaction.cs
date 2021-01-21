namespace DustyBot.Database.Mongo.Models
{
    public class Reaction
    {
        public int Id { get; set; }
        public string Trigger { get; set; }
        public string Value { get; set; }

        public int TriggerCount { get; set; }
    }
}
