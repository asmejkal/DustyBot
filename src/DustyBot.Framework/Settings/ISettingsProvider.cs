﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Framework.Settings
{
    public interface ISettingsProvider
    {
        //Server settings
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

        Task<string> DumpSettings(ulong serverId, string module, bool raw);
        Task SetSettings(string module, string json);
        Task DeleteServer(ulong serverId);

        //Global settings
        Task<T> ReadGlobal<T>()
            where T : new();

        Task ModifyGlobal<T>(Action<T> action)
            where T : new();

        //User settings
        Task<T> ReadUser<T>(ulong userId, bool createIfNeeded = true)
            where T : IUserSettings, new();

        Task<IEnumerable<T>> ReadUser<T>()
            where T : IUserSettings;

        Task ModifyUser<T>(ulong userId, Action<T> action)
            where T : IUserSettings, new();

        Task<U> ModifyUser<T, U>(ulong userId, Func<T, U> action)
            where T : IUserSettings, new();
    }
}
