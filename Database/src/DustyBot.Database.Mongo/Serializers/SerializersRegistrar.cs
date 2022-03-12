using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace DustyBot.Database.Mongo.Serializers
{
    public static class SerializersRegistrar
    {
        public static void RegisterDefaults()
        {
#pragma warning disable CS0618 // Using this to set V3 is not actually obsolete
            BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618 

            BsonSerializer.RegisterSerializer(DateTimeSerializer.LocalInstance);
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new SecureStringSerializer());
        }
    }
}
