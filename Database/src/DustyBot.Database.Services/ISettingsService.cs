﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DustyBot.Database.Core.Settings;

namespace DustyBot.Database.Services
{
    public interface ISettingsService : IDisposable
    {
        Task<T> Read<T>(ulong serverId, bool createIfNeeded = true)
            where T : IServerSettings, new();

        Task<IEnumerable<T>> Read<T>()
            where T : IServerSettings;

        Task Modify<T>(ulong serverId, Action<T> action)
            where T : IServerSettings, new();

        Task<U> Modify<T, U>(ulong serverId, Func<T, U> action)
            where T : IServerSettings, new();

        Task Modify<T>(ulong serverId, Func<T, Task> action)
            where T : IServerSettings, new();

        Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action)
            where T : IServerSettings, new();

        Task<T> ReadGlobal<T>()
            where T : new();

        Task ModifyGlobal<T>(Action<T> action)
            where T : new();

        Task<U> ModifyGlobal<T, U>(Func<T, U> action)
            where T : new();
    }
}
