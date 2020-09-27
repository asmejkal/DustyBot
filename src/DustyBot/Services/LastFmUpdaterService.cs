using DustyBot.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Helpers;
using DustyBot.Framework.Logging;
using Discord;
using System.Threading;
using DustyBot.Database.Services;
using DustyBot.Framework.Services;
using System.Diagnostics;
using System.Collections.Generic;
using DustyBot.Database.Sql.UserDefinedTypes;

namespace DustyBot.Services
{
    internal sealed class LastFmUpdaterService : IService, IDisposable
    {
        private ISettingsService Settings { get; }
        private ILogger Logger { get; }
        private Func<Task<ILastFmStatsService>> LastFmServiceFactory { get; }
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private Task ExecuteTask { get; set; }

        public LastFmUpdaterService(ISettingsService settings, ILogger logger, Func<Task<ILastFmStatsService>> lastFmServiceFactory)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LastFmServiceFactory = lastFmServiceFactory ?? throw new ArgumentNullException(nameof(lastFmServiceFactory));
        }

        public Task StartAsync()
        {
            CancellationTokenSource = new CancellationTokenSource();
            ExecuteTask = Task.Run(() => ExecuteAsync(CancellationTokenSource.Token));
            return Task.CompletedTask;
        }

        private async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Starting Lastfm batch."));
                        await ProcessBatch(ct);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to process Lastfm batch.", ex));
                        await Task.Delay(TimeSpan.FromMinutes(5));
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Fatal error in service", ex));
            }
        }

        private async Task ProcessBatch(CancellationToken ct)
        {
            var settings = (await Settings.ReadUser<LastFmUserSettings>()).ToList();
            var key = (await Settings.ReadGlobal<BotConfig>()).LastFmKey;
            var userDelay = TimeSpan.FromHours(24) / settings.Count;
            var count = 0;

            const int processingLimit = 10;
            using (var semaphore = new SemaphoreSlim(processingLimit))
            {
                var tasks = new List<Task>();
                foreach (var setting in settings)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var stopwatch = Stopwatch.StartNew();

                    await semaphore.WaitAsync(ct);
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Verbose, "Service", $"Processing Lastfm stats for user {setting.LastFmUsername} (count: {Interlocked.Increment(ref count)})."));
                            var client = new LastFmClient(setting.LastFmUsername, key);

                            var topTracks = await client.GetTopTracks(LfStatsPeriod.Overall, ct: ct);

                            using (var dbService = await LastFmServiceFactory())
                            {
                                var table = topTracks
                                    .Where(x => !string.IsNullOrEmpty(x.UrlId) && !string.IsNullOrEmpty(x.Artist?.UrlId))
                                    .Select(x => new SetUserTracksTable(setting.LastFmUsername, x.Playcount, x.UrlId, x.Name, x.Url, x.Artist.UrlId, x.Artist.Name, x.Artist.Url));

                                await dbService.SetUserTracksAsync(table, ct);
                            }
                        }
                        catch (Exception ex)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to process Lastfm stats for user {setting.LastFmUsername}.", ex));
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);

                    var delay = userDelay - stopwatch.Elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);
                }

                await Task.WhenAll(tasks);
            }
        }

        public async Task StopAsync()
        {
            if (CancellationTokenSource != null && ExecuteTask != null)
            {
                CancellationTokenSource.Cancel();
                await ExecuteTask;

                CancellationTokenSource.Dispose();
                CancellationTokenSource = null;
            }
        }

        public void Dispose()
        {
            ((IDisposable)CancellationTokenSource)?.Dispose();
        }
    }

}
