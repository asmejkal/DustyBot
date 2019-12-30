using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Services
{
    public interface IService
    {
        Task Start();
        void Stop();
    }
}
