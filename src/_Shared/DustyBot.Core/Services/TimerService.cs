using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DustyBot.Core.Services
{
    public abstract class TimerService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<TimerService> _logger;
        private readonly TimeSpan _startDelay;
        private readonly TimeSpan _rate;
        private readonly ITimerAwaiter _timerAwaiter;

        protected TimerService(TimeSpan rate, ITimerAwaiter timerAwaiter, IServiceProvider services, ILogger<TimerService> logger, TimeSpan startDelay = default)
        {
            _services = services;
            _logger = logger;
            _startDelay = startDelay;
            _rate = rate;
            _timerAwaiter = timerAwaiter;
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
                            await ExecuteAsync(scope.ServiceProvider, executionCount, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute timed task of service {Service}, count: {ExecutionCount}.", GetType().Name, executionCount);
                    }

                    await _timerAwaiter.WaitTriggerAsync(_rate, stoppingToken);
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

        protected abstract Task ExecuteAsync(IServiceProvider provider, int executionCount, CancellationToken cancellationToken);
    }
}
