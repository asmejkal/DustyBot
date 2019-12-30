using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class SemaphoreSlimExtensions
    {
        public class LockScope : IDisposable
        {
            private SemaphoreSlim Semaphore {get;}

            public LockScope(SemaphoreSlim semaphore)
            {
                Semaphore = semaphore;
            }

            public void Dispose() => Semaphore.Release();
        }

        public static async Task<LockScope> ClaimAsync(this SemaphoreSlim s)
        {
            await s.WaitAsync();
            return new LockScope(s);
        }
    }
}
