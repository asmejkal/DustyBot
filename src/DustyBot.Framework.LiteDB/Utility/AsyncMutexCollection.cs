using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Framework.LiteDB.Utility
{
    public class AsyncMutexCollection<TKey> : IDisposable
    {
        SemaphoreSlim _thisLock = new SemaphoreSlim(1, 1);
        public SemaphoreSlim ThisLock => _thisLock;

        Dictionary<TKey, SemaphoreSlim> _locks = new Dictionary<TKey, SemaphoreSlim>();

        public async Task<SemaphoreSlim> GetOrCreate(TKey key)
        {
            try
            {
                await _thisLock.WaitAsync();

                SemaphoreSlim result;
                if (!_locks.TryGetValue(key, out result))
                    _locks.Add(key, result = new SemaphoreSlim(1, 1));

                return result;
            }
            finally
            {
                _thisLock.Release();
            }
        }

        public async Task InterlockedModify(Func<Dictionary<TKey, SemaphoreSlim>, Task> action)
        {
            try
            {
                await _thisLock.WaitAsync();

                await action(_locks);
            }
            finally
            {
                _thisLock.Release();
            }
        }

        public async Task<T> InterlockedModify<T>(Func<Dictionary<TKey, SemaphoreSlim>, Task<T>> action)
        {
            try
            {
                await _thisLock.WaitAsync();

                return await action(_locks);
            }
            finally
            {
                _thisLock.Release();
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
                    _thisLock.Dispose();
                    _thisLock = null;

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
