using System;

namespace DustyBot.Database.Mongo.Models
{
    public class ProxyBlacklistItem
    {
        public string Address { get; set; }
        public DateTimeOffset Expiration { get; set; }
    }
}
