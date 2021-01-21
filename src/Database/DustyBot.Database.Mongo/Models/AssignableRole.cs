using System.Collections.Generic;

namespace DustyBot.Database.Mongo.Models
{
    public class AssignableRole
    {
        public ulong RoleId { get; set; }
        public List<string> Names { get; set; } = new List<string>();
        public ulong SecondaryId { get; set; }
        public HashSet<string> Groups { get; set; } = new HashSet<string>();
    }
}
