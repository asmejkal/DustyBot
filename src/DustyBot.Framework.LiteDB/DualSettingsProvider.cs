using DustyBot.Framework.Logging;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.LiteDB
{
    public class DualSettingsProvider : ISettingsProvider
    {
        public class PerformanceInfo
        {
            public int ReadRequestCount { get; set; }
            public int WriteRequestCount { get; set; }
            public TimeSpan TotalReadRequestLength { get; set; }
            public TimeSpan TotalWriteRequestLength { get; set; }

            public TimeSpan AverageRead => ReadRequestCount > 0 ? TotalReadRequestLength / ReadRequestCount : TimeSpan.Zero;
            public TimeSpan AverageWrite => WriteRequestCount > 0 ? TotalWriteRequestLength / WriteRequestCount : TimeSpan.Zero;

            public PerformanceInfo()
            {
            }

            public PerformanceInfo(PerformanceInfo other)
            {
                ReadRequestCount = other.ReadRequestCount;
                WriteRequestCount = other.WriteRequestCount;
                TotalReadRequestLength = other.TotalReadRequestLength;
                TotalWriteRequestLength = other.TotalWriteRequestLength;
            }
        }

        private SettingsProvider LiteDbProvider { get; }
        private MongoDbSettingsProvider MongoDbProvider { get; }
        private ILogger Logger { get; }

        private PerformanceInfo MongoPerformanceInfo { get; } = new PerformanceInfo();
        private PerformanceInfo LitePerformanceInfo { get; } = new PerformanceInfo();

        private object ContextLock { get; } = new object();

        public DualSettingsProvider(SettingsProvider liteDbProvider, MongoDbSettingsProvider mongoDbProvider, ILogger logger)
        {
            LiteDbProvider = liteDbProvider ?? throw new ArgumentNullException(nameof(liteDbProvider));
            MongoDbProvider = mongoDbProvider ?? throw new ArgumentNullException(nameof(mongoDbProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public (PerformanceInfo LiteDb, PerformanceInfo MongoDb) GetPerformanceInfo()
        {
            lock (ContextLock)
            {
                return (new PerformanceInfo(LitePerformanceInfo), new PerformanceInfo(MongoPerformanceInfo));
            }
        }

        public Task DeleteServer(ulong serverId)
        {
            return Call(x => x.DeleteServer(serverId), true);
        }

        public Task<string> DumpSettings(ulong serverId, string module, bool raw)
        {
            return Call(x => x.DumpSettings(serverId, module, raw), false);
        }

        public Task Modify<T>(ulong serverId, Action<T> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action), true);
        }

        public Task<U> Modify<T, U>(ulong serverId, Func<T, U> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action), true);
        }

        public Task Modify<T>(ulong serverId, Func<T, Task> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action), true);
        }

        public Task<U> Modify<T, U>(ulong serverId, Func<T, Task<U>> action) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Modify(serverId, action), true);
        }

        public Task ModifyGlobal<T>(Action<T> action)
            where T : new()
        {
            return Call(x => x.ModifyGlobal(action), true);
        }

        public Task ModifyUser<T>(ulong userId, Action<T> action)
            where T : IUserSettings, new()
        {
            return Call(x => x.ModifyUser(userId, action), true);
        }

        public Task<U> ModifyUser<T, U>(ulong userId, Func<T, U> action) 
            where T : IUserSettings, new()
        {
            return Call(x => x.ModifyUser(userId, action), true);
        }

        public Task<T> Read<T>(ulong serverId, bool createIfNeeded = true) 
            where T : IServerSettings, new()
        {
            return Call(x => x.Read<T>(serverId, createIfNeeded), false);
        }

        public Task<IEnumerable<T>> Read<T>() 
            where T : IServerSettings
        {
            return Call(x => x.Read<T>(), false);
        }

        public Task<T> ReadGlobal<T>() 
            where T : new()
        {
            return Call(x => x.ReadGlobal<T>(), false);
        }

        public Task<T> ReadUser<T>(ulong userId, bool createIfNeeded = true) 
            where T : IUserSettings, new()
        {
            return Call(x => x.ReadUser<T>(userId, createIfNeeded), false);
        }

        public Task<IEnumerable<T>> ReadUser<T>() 
            where T : IUserSettings
        {
            return Call(x => x.ReadUser<T>(), false);
        }

        public Task SetSettings(string module, string json)
        {
            return Call(x => x.SetSettings(module, json), true);
        }

        private Task Call(Func<ISettingsProvider, Task> call, bool write)
        {
            FireSilentCall(() => MeasuredSingleCall(MongoDbProvider, call, MongoPerformanceInfo, write));
            return MeasuredSingleCall(LiteDbProvider, call, LitePerformanceInfo, write);
        }

        private Task<T> Call<T>(Func<ISettingsProvider, Task<T>> call, bool write)
        {
            FireSilentCall(() => MeasuredSingleCall(MongoDbProvider, call, MongoPerformanceInfo, write));
            return MeasuredSingleCall(LiteDbProvider, call, LitePerformanceInfo, write);
        }

        private async Task MeasuredSingleCall(ISettingsProvider provider, Func<ISettingsProvider, Task> call, PerformanceInfo performanceInfo, bool write)
        {
            var stopwatch = Stopwatch.StartNew();
            await call(provider);
            var elapsed = stopwatch.Elapsed;

            lock (ContextLock)
            {
                if (write)
                {
                    performanceInfo.WriteRequestCount++;
                    performanceInfo.TotalWriteRequestLength += elapsed;
                }
                else
                {
                    performanceInfo.ReadRequestCount++;
                    performanceInfo.TotalReadRequestLength += elapsed;
                }
            }
        }

        private async Task<T> MeasuredSingleCall<T>(ISettingsProvider provider, Func<ISettingsProvider, Task<T>> call, PerformanceInfo performanceInfo, bool write)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await call(provider);
            var elapsed = stopwatch.Elapsed;

            lock (ContextLock)
            {
                if (write)
                {
                    performanceInfo.WriteRequestCount++;
                    performanceInfo.TotalWriteRequestLength += elapsed;
                }
                else
                {
                    performanceInfo.ReadRequestCount++;
                    performanceInfo.TotalReadRequestLength += elapsed;
                }
            }

            return result;
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
