using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Collections
{
    public class ProxyBlacklistItem
    {
        public string Address { get; set; }
        public DateTimeOffset Expiration { get; set; }
    }

    public class ProxyList : BaseGlobalSettings
    {
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, ProxyBlacklistItem> Blacklist { get; set; } = new Dictionary<string, ProxyBlacklistItem>();
    }
}
