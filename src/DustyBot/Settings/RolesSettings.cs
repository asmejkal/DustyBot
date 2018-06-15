using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings
{
    public class AssignableRole
    {
        public ulong RoleId { get; set; }
        public List<string> Names { get; set; } = new List<string>();
        public ulong SecondaryId { get; set; }
    }

    public class RolesSettings : ServerSettings
    {
        public ulong RoleChannel { get; set; }
        public bool ClearRoleChannel { get; set; }
        public List<AssignableRole> AssignableRoles { get; set; } = new List<AssignableRole>();
    }
}
