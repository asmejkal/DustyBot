using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace DustyBot.Database.Mongo.Serializers
{
    public static class SerializersRegistrar
    {
        public static void RegisterDefaults()
        {
            BsonSerializer.RegisterSerializer(DateTimeSerializer.LocalInstance);
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));
            BsonSerializer.RegisterSerializer(new SecureStringSerializer());
        }
    }
}
