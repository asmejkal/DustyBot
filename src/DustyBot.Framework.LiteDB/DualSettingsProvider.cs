using DustyBot.Framework.Logging;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.LiteDB
{
    public class DualSettingsProvider : ISettingsProvider
    {
        private SettingsProvider LiteDbProvider { get; }
        private MongoDbSettingsProvider MongoDbProvider { get; }
        private ILogger Logger { get; }

        public DualSettingsProvider(SettingsProvider liteDbProvider, MongoDbSettingsProvider mongoDbProvider, ILogger logger)
        {
            LiteDbProvider = liteDbProvider ?? throw new ArgumentNullException(nameof(liteDbProvider));
            MongoDbProvider = mongoDbProvider ?? throw new ArgumentNullException(nameof(mongoDbProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task DeleteServer(ulong serverId)
        {
            return Call(x => x.DeleteServer(serverId));
        }

        public Task<string> DumpSettings(ulong serverId, string module, bool raw)
        {
            return Call(x => x.DumpSettings(serverId, module, raw));
        }

        public Task Modify<T>(ulong serverId, Action<T> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action));
        }

        public Task<U> Modify<T, U>(ulong serverId, Func<T, U> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action));
        }

        public Task Modify<T>(ulong serverId, Func<T, Task> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action));
        }

        public Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action));
        }

        public Task ModifyGlobal<T>(Action<T> action) 
            where T : new()
        {
            return Call(x => x.ModifyGlobal(action));
        }

        public Task ModifyUser<T>(ulong userId, Action<T> action)
            where T : IUserSettings, new()
        {
            return Call(x => x.ModifyUser(userId, action));
        }

        public Task<U> ModifyUser<T, U>(ulong userId, Func<T, U> action) 
            where T : IUserSettings, new()
        {
            return Call(x => x.ModifyUser(userId, action));
        }

        public Task<T> Read<T>(ulong serverId, bool createIfNeeded = true) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Read<T>(serverId, createIfNeeded));
        }

        public Task<IEnumerable<T>> Read<T>() 
            where T : IServerSettings
        {
            return Call(x => x.Read<T>());
        }

        public Task<T> ReadGlobal<T>() 
            where T : new()
        {
            return Call(x => x.ReadGlobal<T>());
        }

        public Task<T> ReadUser<T>(ulong userId, bool createIfNeeded = true) 
            where T : IUserSettings, new()
        {
            return Call(x => x.ReadUser<T>(userId, createIfNeeded));
        }

        public Task<IEnumerable<T>> ReadUser<T>() 
            where T : IUserSettings
        {
            return Call(x => x.ReadUser<T>());
        }

        public Task SetSettings(string module, string json)
        {
            return Call(x => x.SetSettings(module, json));
        }

        private Task Call(Func<ISettingsProvider, Task> call)
        {
            FireSilentCall(() => call(LiteDbProvider));
            return call(MongoDbProvider);
        }

        private Task<T> Call<T>(Func<ISettingsProvider, Task<T>> call)
        {
            FireSilentCall(() => call(LiteDbProvider));
            return call(MongoDbProvider);
        }

        private void FireSilentCall(Func<Task> call)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    await call();
                }
                catch (Exception ex)
                {
                    await Logger.Log(new Discord.LogMessage(Discord.LogSeverity.Error, "Settings", "Exception encountered", ex));
                }
            });
        }
    }
}
