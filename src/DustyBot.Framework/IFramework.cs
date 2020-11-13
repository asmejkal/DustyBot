using System;
using System.Threading.Tasks;

namespace DustyBot.Framework
{
    public interface IFramework : IDisposable
    {
        Task StartAsync();
    }
}
