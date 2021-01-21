using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Collections
{
    public class ProxyList : BaseGlobalSettings
    {
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, ProxyBlacklistItem> Blacklist { get; set; } = new Dictionary<string, ProxyBlacklistItem>();
    }
}
