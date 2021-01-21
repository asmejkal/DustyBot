using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Framework
{
    public interface IFramework : IDisposable
    {
        Task StartAsync(CancellationToken ct);
    }
}
