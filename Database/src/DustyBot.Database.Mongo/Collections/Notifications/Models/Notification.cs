using System;
using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Collections.Notifications.Models
{
    public class Notification : IEquatable<Notification?>
    {
        public string Keyword { get; set; }
        public ulong User { get; set; }
        public int TriggerCount { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Notification()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public Notification(string keyword, ulong user, int triggerCount = 0)
        {
            Keyword = !string.IsNullOrEmpty(keyword) ? keyword : throw new ArgumentNullException(nameof(keyword));
            User = user;
            TriggerCount = triggerCount;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Notification);
        }

        public bool Equals(Notification? other)
        {
            return other != null &&
                   Keyword == other.Keyword &&
                   User == other.User;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Keyword, User);
        }

        public static bool operator ==(Notification? left, Notification? right)
        {
            return EqualityComparer<Notification>.Default.Equals(left, right);
        }

        public static bool operator !=(Notification? left, Notification? right)
        {
            return !(left == right);
        }
    }
}
