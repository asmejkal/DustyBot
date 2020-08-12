using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Framework.MongoDb.Utility
{
    public class AsyncMutexCollection<TKey> : IDisposable
    {
        public SemaphoreSlim ThisLock { get; private set; } = new SemaphoreSlim(1, 1);

        Dictionary<TKey, SemaphoreSlim> _locks = new Dictionary<TKey, SemaphoreSlim>();

        public async Task<SemaphoreSlim> GetOrCreate(TKey key)
        {
            try
            {
                await ThisLock.WaitAsync();

                SemaphoreSlim result;
                if (!_locks.TryGetValue(key, out result))
                    _locks.Add(key, result = new SemaphoreSlim(1, 1));

                return result;
            }
            finally
            {
                ThisLock.Release();
            }
        }

        public async Task InterlockedModify(Func<Dictionary<TKey, SemaphoreSlim>, Task> action)
        {
            try
            {
                await ThisLock.WaitAsync();

                await action(_locks);
            }
            finally
            {
                ThisLock.Release();
            }
        }

        public async Task<T> InterlockedModify<T>(Func<Dictionary<TKey, SemaphoreSlim>, Task<T>> action)
        {
            try
            {
                await ThisLock.WaitAsync();

                return await action(_locks);
            }
            finally
            {
                ThisLock.Release();
            }
        }


        #region IDisposable 

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ThisLock.Dispose();
                    ThisLock = null;

                    foreach (var l in _locks)
                        l.Value.Dispose();
                }

                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }
}
