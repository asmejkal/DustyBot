using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Core.Settings;

namespace DustyBot.Database.Services
{
    public interface ISettingsService : IDisposable
    {
        Task<T> Read<T>(ulong serverId, bool createIfNeeded = true, CancellationToken ct = default)
            where T : IServerSettings, new();

        Task<IEnumerable<T>> Read<T>(CancellationToken ct = default)
            where T : IServerSettings;

        Task Modify<T>(ulong serverId, Action<T> action, CancellationToken ct = default)
            where T : IServerSettings, new();

        Task<U> Modify<T, U>(ulong serverId, Func<T, U> action, CancellationToken ct = default)
            where T : IServerSettings, new();

        Task Modify<T>(ulong serverId, Func<T, Task> action, CancellationToken ct = default)
            where T : IServerSettings, new();

        Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action, CancellationToken ct = default)
            where T : IServerSettings, new();

        Task<T> ReadGlobal<T>(CancellationToken ct = default)
            where T : new();

        Task ModifyGlobal<T>(Action<T> action, CancellationToken ct = default)
            where T : new();

        Task<U> ModifyGlobal<T, U>(Func<T, U> action, CancellationToken ct = default)
            where T : new();
    }
}
