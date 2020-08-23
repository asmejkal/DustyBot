using Discord.WebSocket;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Helpers;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using Discord;
using System.Threading;
using System.Collections.Concurrent;
using System.Text;
using static DustyBot.Framework.LiteDB.DualSettingsProvider;
using DustyBot.Framework.Services;

namespace DustyBot.Services
{
    class ReportingService : IService
    {
        private ISettingsProvider Settings { get; }
        private ILogger Logger { get; }

        public static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(5); 

        private bool Updating { get; set; }
        private object UpdatingLock = new object();

        private Timer UpdateTimer { get; set; }

        private PerformanceInfo LastMongoDbInfo { get; set; } = new PerformanceInfo();
        private PerformanceInfo LastLiteDbInfo { get; set; } = new PerformanceInfo();

        public ReportingService(ISettingsProvider settings, ILogger logger)
        {
            Settings = settings;
            Logger = logger;
        }

        public Task Start()
        {
            UpdateTimer = new Timer(OnUpdate, null, (int)UpdateFrequency.TotalMilliseconds, (int)UpdateFrequency.TotalMilliseconds);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            UpdateTimer?.Dispose();
            UpdateTimer = null;
        }

        void OnUpdate(object state)
        {
            TaskHelper.FireForget(async () =>
            {
                lock (UpdatingLock)
                {
                    if (Updating)
                        return;

                    Updating = true;
                }

                try
                {
                    if (Settings is Framework.LiteDB.DualSettingsProvider dualSettings)
                    {
                        void PrintStats(StringBuilder builder, PerformanceInfo info, PerformanceInfo last, string name)
                        {
                            builder.AppendLine($"{name} ");
                            var readCount = info.ReadRequestCount - last.ReadRequestCount;
                            var writeCount = info.WriteRequestCount - last.WriteRequestCount;
                            builder.AppendLine($"Reads: {info.ReadRequestCount - last.ReadRequestCount} in {info.TotalReadRequestLength - last.TotalReadRequestLength} (avg {(readCount > 0 ? ((info.TotalReadRequestLength - last.TotalReadRequestLength) / readCount) : TimeSpan.Zero).TotalMilliseconds}ms) ");
                            builder.AppendLine($"Writes: {info.WriteRequestCount - last.WriteRequestCount} in {info.TotalWriteRequestLength - last.TotalWriteRequestLength} (avg {(writeCount > 0 ? ((info.TotalWriteRequestLength - last.TotalWriteRequestLength) / writeCount) : TimeSpan.Zero).TotalMilliseconds}ms) ");
                        }

                        var stats = dualSettings.GetPerformanceInfo();

                        var result = new StringBuilder();
                        PrintStats(result, stats.LiteDb, LastLiteDbInfo, "LiteDb");
                        PrintStats(result, stats.MongoDb, LastMongoDbInfo, "MongoDb");

                        await Logger.Log(new LogMessage(LogSeverity.Error, "Reporting", $"Performance: \n{result.ToString()}"));

                        LastLiteDbInfo = stats.LiteDb;
                        LastMongoDbInfo = stats.MongoDb;
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Reporting", "Failed to report.", ex));
                }
                finally
                {
                    Updating = false;
                }
            });
        }

        #region IDisposable 

        private bool _disposed = false;
                
        public void Dispose()
        {
            Dispose(true);
        }
                
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UpdateTimer?.Dispose();
                    UpdateTimer = null;
                }
                
                _disposed = true;
            }
        }

        #endregion
    }

}
