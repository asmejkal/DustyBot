using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Models
{
    public class ProxyList : BaseGlobalSettings
    {
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, ProxyBlacklistItem> Blacklist { get; set; } = new Dictionary<string, ProxyBlacklistItem>();
    }
}
