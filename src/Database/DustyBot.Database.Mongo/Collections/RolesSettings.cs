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
        public List<AssignableRole> AssignableRoles { get; set; } = new List<AssignableRole>();

        public HashSet<ulong> AutoAssignRoles { get; set; } = new HashSet<ulong>();

        public bool PersistentAssignableRoles { get; set; }
        public List<ulong> AdditionalPersistentRoles { get; set; } = new List<ulong>();

        public Dictionary<string, GroupSettings> GroupSettings { get; set; } = new Dictionary<string, GroupSettings>();

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<ulong, List<ulong>> PersistentRolesData { get; set; } = new Dictionary<ulong, List<ulong>>();
    }
}
