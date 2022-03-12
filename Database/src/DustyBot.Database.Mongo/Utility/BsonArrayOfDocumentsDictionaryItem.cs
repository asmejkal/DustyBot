using System.Linq;
using MongoDB.Bson;

namespace DustyBot.Database.Mongo.Utility
{
    public class BsonArrayOfDocumentsDictionaryItem : BsonDocument
    {
        public BsonArrayOfDocumentsDictionaryItem(BsonValue key, BsonValue value)
            : base(new[] { new BsonElement("k", key), new BsonElement("v", value) }.AsEnumerable())
        {
        }
    }
}
