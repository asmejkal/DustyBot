using System;

namespace DustyBot.Core.Disposal
{
    public sealed class DisposableWrapper : IDisposable
    {
        private readonly IDisposable[] _inner;

        public DisposableWrapper(params IDisposable[] inner)
        {
            _inner = inner;
        }

        public void Dispose()
        {
            foreach (var item in _inner)
                item.Dispose();
        }
    }
}
