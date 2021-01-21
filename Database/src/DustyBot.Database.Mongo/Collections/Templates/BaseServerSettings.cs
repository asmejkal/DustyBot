using DustyBot.Database.Core.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Collections.Templates
{
    public abstract class BaseServerSettings : IServerSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.Int64, AllowOverflow = true)]
        public ulong ServerId { get; set; }
    }
}
