using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Settings
{
    public class AssignableRole
    {
        public ulong RoleId { get; set; }
        public List<string> Names { get; set; } = new List<string>();
        public ulong SecondaryId { get; set; }
    }

    interface IRolesSettings : Framework.Settings.IServerSettings
    {
        ulong RoleChannel { get; set; }
        bool ClearRoleChannel { get; set; }
        List<AssignableRole> AssignableRoles { get; set; }
    }
}
