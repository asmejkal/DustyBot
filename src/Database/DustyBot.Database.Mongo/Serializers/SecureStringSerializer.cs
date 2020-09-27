using DustyBot.Core.Security;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Security;

namespace DustyBot.Database.Mongo.Serializers
{
    public class SecureStringSerializer : SerializerBase<SecureString>
    {
        public override SecureString Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return context.Reader.ReadBytes().ToSecureString();
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, SecureString value)
        {
            context.Writer.WriteBytes(value.ToByteArray());
        }
    }
}
