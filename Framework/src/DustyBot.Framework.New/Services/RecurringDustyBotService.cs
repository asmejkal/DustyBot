using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Services
{
    public abstract class RecurringDustyBotService : DustyBotService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<RecurringDustyBotService> _logger;
        private readonly TimeSpan _startDelay;
        private readonly TimeSpan _delay;
        private readonly ITimerAwaiter _timerAwaiter;

        protected RecurringDustyBotService(TimeSpan delay, ITimerAwaiter timerAwaiter, IServiceProvider services, ILogger<RecurringDustyBotService> logger, TimeSpan startDelay = default)
        {
            _services = services;
            _logger = logger;
            _startDelay = startDelay;
            _delay = delay;
            _timerAwaiter = timerAwaiter;
        }

        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _timerAwaiter.DelayAsync(_startDelay, stoppingToken);

                int executionCount = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    var count = Interlocked.Increment(ref executionCount);
                    _logger.LogInformation("Executing recurring task of service {ServiceType}, count: {ExecutionCount}", GetType().Name, executionCount);

                    try
                    {
                        using (var scope = _services.CreateScope())
                        {
                            await ExecuteRecurringAsync(scope.ServiceProvider, executionCount, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute recurring task of service {ServiceType}, count: {ExecutionCount}", GetType().Name, executionCount);
                    }

                    await _timerAwaiter.DelayAsync(_delay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring task service fatal failure");
            }
        }

        protected abstract Task ExecuteRecurringAsync(IServiceProvider provider, int executionCount, CancellationToken ct);
    }
}
