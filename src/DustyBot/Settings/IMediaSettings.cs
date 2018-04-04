using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Settings
{
    public class ComebackInfo
    {
        public string Name { get; set; }
        public HashSet<string> VideoIds { get; set; } = new HashSet<string>();
    }

    interface IMediaSettings : Framework.Settings.IServerSettings
    {
        List<ComebackInfo> YouTubeComebacks { get; set; }
    }
}
