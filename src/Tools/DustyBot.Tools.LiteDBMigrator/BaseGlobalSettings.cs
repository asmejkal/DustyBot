using System;
using System.Collections.Generic;
using System.Text;
using DustyBot.Framework.Settings;
using LiteDB;

namespace DustyBot.Framework.LiteDB
{
    public abstract class BaseGlobalSettings
    {
        [BsonId]
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(MongoDB.Bson.BsonType.Int64, AllowOverflow = true)]
        public ulong Id { get; set; } = Definitions.GlobalSettingsId;
    }
}
