using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DustyBot.Core.Services
{
    public abstract class RecurringTaskService : BackgroundService, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<RecurringTaskService> _logger;
        private readonly TimeSpan _startDelay;
        private readonly TimeSpan _delay;
        private readonly ITimerAwaiter _timerAwaiter;
        private readonly TaskCompletionSource<byte> _firstExecutionTaskSource = new TaskCompletionSource<byte>();

        protected RecurringTaskService(TimeSpan delay, ITimerAwaiter timerAwaiter, IServiceProvider services, ILogger<RecurringTaskService> logger, TimeSpan startDelay = default)
        {
            _services = services;
            _logger = logger;
            _startDelay = startDelay;
            _delay = delay;
            _timerAwaiter = timerAwaiter;
        }

        public override Task StopAsync(CancellationToken ct)
        {
            if (!_firstExecutionTaskSource.Task.IsCompleted)
                _firstExecutionTaskSource.SetCanceled();

            return Task.CompletedTask;
        }

        protected async Task WaitForFirstCompletion()
        {
            await _firstExecutionTaskSource.Task;
        }

        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _timerAwaiter.WaitTriggerAsync(_startDelay, stoppingToken);

                int executionCount = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    var count = Interlocked.Increment(ref executionCount);
                    _logger.LogInformation("Executing timed task of service {Service}, count: {ExecutionCount}.", GetType().Name, executionCount);

                    try
                    {
                        using (var scope = _services.CreateScope())
                        {
                            await ExecuteRecurringAsync(scope.ServiceProvider, executionCount, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute timed task of service {Service}, count: {ExecutionCount}.", GetType().Name, executionCount);
                    }
                    finally
                    {
                        if (!_firstExecutionTaskSource.Task.IsCompleted)
                            _firstExecutionTaskSource.SetResult(0);
                    }

                    await _timerAwaiter.WaitTriggerAsync(_delay, stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Stopped
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Timer service fatal failure.");
            }
        }

        protected abstract Task ExecuteRecurringAsync(IServiceProvider provider, int executionCount, CancellationToken ct);

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (!_firstExecutionTaskSource.Task.IsCompleted)
                        _firstExecutionTaskSource.SetCanceled();
                }

                _disposedValue = true;
            }
        }

        public override void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
