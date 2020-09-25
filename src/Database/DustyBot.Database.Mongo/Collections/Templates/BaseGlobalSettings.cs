using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DustyBot.Database.Mongo.Collections.Templates
{
    public abstract class BaseGlobalSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.Int64, AllowOverflow = true)]
        public ulong Id { get; set; } = CollectionConstants.GlobalSettingId;
    }
}
