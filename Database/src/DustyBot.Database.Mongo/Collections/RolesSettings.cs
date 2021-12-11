using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Collections
{
    public class RolesSettings : BaseServerSettings
    {
        public ulong RoleChannel { get; set; }
        public bool ClearRoleChannel { get; set; }
        public List<AssignableRole> AssignableRoles { get; set; } = new();

        public HashSet<ulong> AutoAssignRoles { get; set; } = new();

        public bool PersistentAssignableRoles { get; set; }
        public List<ulong> AdditionalPersistentRoles { get; set; } = new();

        [BsonDictionaryOptions(Representation = DictionaryRepresentation.Document)]
        public Dictionary<string, GroupSettings> GroupSettings { get; set; } = new();

        public Dictionary<ulong, List<ulong>> PersistentRolesData { get; set; } = new();
    }
}
