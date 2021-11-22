using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Core.Async
{
    public sealed class KeyedSemaphoreSlim<TKey> : IDisposable
    {
        private class KeyLockScope : IDisposable
        {
            private readonly int _initialCount;
            private readonly Action<KeyLockScope> _parentRelease;
            private readonly SemaphoreSlim _semaphore;

            private int _counter;

            public TKey Key { get; private set; }

            public KeyLockScope(TKey key, int initialCount, Action<KeyLockScope> parentRelease)
            {
                Key = key;
                _initialCount = _counter = initialCount;
                _parentRelease = parentRelease;
                _semaphore = new SemaphoreSlim(initialCount, initialCount);
            }

            public KeyLockScope Reuse(TKey key)
            {
                if (_counter != _initialCount)
                    throw new InvalidOperationException("Can't reuse instance because counter is not at initial count.");

                Key = key;
                return this;
            }

            public async Task<IDisposable> WaitAsync(CancellationToken ct)
            {
                _counter--;
                await _semaphore.WaitAsync(ct).ConfigureAwait(false);
                return this;
            }

            public bool Release()
            {
                _semaphore.Release();
                _counter++;
                return _counter == _initialCount;
            }

            public void Dispose()
            {
                _parentRelease(this);
            }

            public void InternalDispose()
            {
                _semaphore.Dispose();
            }
        }

        private readonly int _initialCount;
        private readonly int _maxPoolSize;
        private readonly object _lock = new object();
        private readonly Queue<KeyLockScope> _pool = new Queue<KeyLockScope>();
        private readonly Dictionary<TKey, KeyLockScope> _scopes = new Dictionary<TKey, KeyLockScope>();

        private bool _isDisposed;

        public KeyedSemaphoreSlim(int initialCount, int maxPoolSize = 100)
        {
            if (initialCount < 1)
                throw new ArgumentException(nameof(initialCount));

            if (maxPoolSize < 0)
                throw new ArgumentException(nameof(maxPoolSize));

            _initialCount = initialCount;
            _maxPoolSize = maxPoolSize;
        }

        public Task<IDisposable> ClaimAsync(TKey key, CancellationToken ct = default)
        {
            lock (_lock)
            {
                KeyLockScope scope;

                if (_scopes.ContainsKey(key))
                {
                    scope = _scopes[key];
                }
                else
                {
                    scope = _pool.Count == 0 ? new KeyLockScope(key, _initialCount, Release) : _pool.Dequeue().Reuse(key);
                    _scopes[key] = scope;
                }

                return scope.WaitAsync(ct);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            lock (_lock)
            {
                foreach (var scope in _scopes.Values)
                    scope.InternalDispose();

                _scopes.Clear();
                foreach (var scope in _pool)
                    scope.InternalDispose();

                _pool.Clear();
            }

            _isDisposed = true;
        }

        private void Release(KeyLockScope scope)
        {
            lock (_lock)
            {
                if (!scope.Release())
                    return;

                _scopes.Remove(scope.Key);
                if (_pool.Count < _maxPoolSize)
                    _pool.Enqueue(scope);
                else
                    scope.InternalDispose();
            }
        }
    }
}
