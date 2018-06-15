using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Services
{
    public interface IServiceCollection
    {
        IEnumerable<IService> Services { get; }
    }
}
