using MongoDB.Bson;

namespace DustyBot.Database.Mongo.Utility
{
    public class BsonArrayOfDocumentsDictionaryItem : BsonDocument
    {
        public BsonArrayOfDocumentsDictionaryItem(string key, BsonValue value)
        {
            Add("k", key);
            Add("v", value);
        }
    }
}
