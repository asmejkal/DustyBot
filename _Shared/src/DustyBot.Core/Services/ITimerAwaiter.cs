using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Core.Services
{
    public interface ITimerAwaiter
    {
        Task WaitTriggerAsync(TimeSpan delay, CancellationToken ct);
    }
}
