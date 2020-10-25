using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Settings
{
    public class BotConfig : BaseGlobalSettings
    {
        public string CommandPrefix { get; set; }
        public string BotToken { get; set; }
        public List<ulong> OwnerIDs { get; set; } = new List<ulong>();
        public string YouTubeKey { get; set; }
        public GoogleAccountCredentials GCalendarSAC { get; set; }
        public string LastFmKey { get; set; }
        public string SpotifyId { get; set; }
        public string SpotifyKey { get; set; }
        public string TableStorageConnectionString { get; set; }
        public string SqlDbConnectionString { get; set; }
        public string PapagoClientId { get; set; }
        public string PapagoClientSecret { get; set; }
        public string PolrKey { get; set; }
        public string PolrDomain { get; set; }
        
        [BsonIgnore]
        public string BitlyKey { get => ShortenerKey; set => ShortenerKey = value; }
        public string ShortenerKey { get; set; }
    }
}
