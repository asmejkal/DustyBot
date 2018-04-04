using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Logging
{
    public interface ILogger
    {
        Task Log(Discord.LogMessage message);
    }
}
