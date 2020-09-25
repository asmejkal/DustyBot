using DustyBot.Database.Mongo.Collections.Templates;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System.Collections.Generic;

namespace DustyBot.Settings
{
    public class AssignableRole
    {
        public ulong RoleId { get; set; }
        public List<string> Names { get; set; } = new List<string>();
        public ulong SecondaryId { get; set; }
        public HashSet<string> Groups { get; set; } = new HashSet<string>();
    }

    public class GroupSettings
    {
        public uint Limit { get; set; }
    }

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
