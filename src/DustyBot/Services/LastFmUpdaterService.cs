using DustyBot.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Logging;
using Discord;
using System.Threading;
using DustyBot.Database.Services;
using System.Diagnostics;
using System.Collections.Generic;
using DustyBot.Database.Sql.UserDefinedTypes;
using DustyBot.LastFm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DustyBot.Configuration;

namespace DustyBot.Services
{
    internal sealed class LastFmUpdaterService : BackgroundService
    {
        private readonly ISettingsService _settings;
        private readonly ILogger<LastFmUpdaterService> _logger;
        private readonly Func<Task<ILastFmStatsService>> _lastFmServiceFactory;
        private readonly IOptions<IntegrationOptions> _integrationOptions;

        public LastFmUpdaterService(
            ISettingsService settings, 
            ILogger<LastFmUpdaterService> logger, 
            Func<Task<ILastFmStatsService>> lastFmServiceFactory,
            IOptions<IntegrationOptions> integrationOptions)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lastFmServiceFactory = lastFmServiceFactory ?? throw new ArgumentNullException(nameof(lastFmServiceFactory));
            _integrationOptions = integrationOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogInformation("Starting Lastfm batch");
                        await ProcessBatch(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // Stopping
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process Lastfm batch");
                        await Task.Delay(TimeSpan.FromMinutes(5));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in service");
            }
        }

        private async Task ProcessBatch(CancellationToken ct)
        {
            var settings = (await _settings.ReadUser<LastFmUserSettings>()).ToList();
            var key = _integrationOptions.Value.LastFmKey;
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
                            var countSnapshot = Interlocked.Increment(ref count);
                            _logger.LogInformation("Processing Lastfm stats for user {LastFmUsername} ({" + LogFields.UserId +"}) (count: {Count})", setting.LastFmUsername, setting.UserId, countSnapshot);
                            var client = new LastFmClient(setting.LastFmUsername, key);

                            var topTracks = await client.GetTopTracks(LastFmDataPeriod.Overall, ct: ct);

                            using (var dbService = await _lastFmServiceFactory())
                            {
                                var table = topTracks
                                    .Where(x => !string.IsNullOrEmpty(x.HashId) && !string.IsNullOrEmpty(x.Artist?.HashId))
                                    .Select(x => new SetUserTracksTable(setting.LastFmUsername, x.Playcount.Value, x.HashId, x.Name, x.Url, x.Artist.HashId, x.Artist.Name, x.Artist.Url));

                                await dbService.SetUserTracksAsync(table, ct);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process Lastfm stats for user {LastFmUsername} ({" + LogFields.UserId + "})", setting.LastFmUsername, setting.UserId);
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
    }

}
