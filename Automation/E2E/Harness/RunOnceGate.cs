using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Automation.E2E.Harness
{
    /// <summary>Runs an async action exactly once, however many times RunOnceAsync is called.</summary>
    public sealed class RunOnceGate
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _hasRun;

        public async Task RunOnceAsync(Func<Task> action)
        {
            if (_hasRun)
                return;

            await _gate.WaitAsync();
            try
            {
                if (_hasRun)
                    return;

                await action();
                _hasRun = true;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
