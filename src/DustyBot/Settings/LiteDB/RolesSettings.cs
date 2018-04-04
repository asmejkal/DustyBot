using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;

namespace DustyBot.Settings.LiteDB
{
    public class RolesSettings : ServerSettings, IRolesSettings
    {
        public ulong RoleChannel { get; set; }
        public bool ClearRoleChannel { get; set; }
        public List<AssignableRole> AssignableRoles { get; set; } = new List<AssignableRole>();
    }
}
