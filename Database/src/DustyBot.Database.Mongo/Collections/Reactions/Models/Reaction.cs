using System;

namespace DustyBot.Database.Mongo.Collections.Reactions.Models
{
    public class Reaction
    {
        public int Id { get; set; }
        public string Trigger { get; set; }
        public string Value { get; set; }

        public TimeSpan Cooldown { get; set; }
        public DateTimeOffset LastUsage { get; set; }

        public int TriggerCount { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Reaction()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public Reaction(int id, string trigger, string value, TimeSpan cooldown = default)
        {
            Id = id;
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Cooldown = cooldown;
        }
    }
}
