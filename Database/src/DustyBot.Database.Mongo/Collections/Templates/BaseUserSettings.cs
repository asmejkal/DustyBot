using DustyBot.Database.Core.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Collections.Templates
{
    public abstract class BaseUserSettings : IUserSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.Int64, AllowOverflow = true)]
        public ulong UserId { get; set; }
    }
}
