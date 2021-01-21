using System.Collections.Generic;
using System.Linq;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public class NotificationSettings : BaseServerSettings
    {
        public List<Notification> Notifications { get; set; } = new List<Notification>();

        public bool RaiseCount(ulong user, string loweredWord)
        {
            var n = Notifications.FirstOrDefault(x => x.User == user && x.LoweredWord == loweredWord);
            if (n != null)
                n.TriggerCount++;

            return n != null;
        }
    }
}
