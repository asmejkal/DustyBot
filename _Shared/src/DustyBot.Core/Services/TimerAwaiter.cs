using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Core.Services
{
    public class TimerAwaiter : ITimerAwaiter
    {
        public Task WaitTriggerAsync(TimeSpan delay, CancellationToken ct) =>
            Task.Delay(delay, ct);
    }
}
