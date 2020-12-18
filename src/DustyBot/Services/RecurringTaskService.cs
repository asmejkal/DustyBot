using Discord;
using DustyBot.Framework.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    internal abstract class RecurringTaskService : BackgroundService
    {
        private readonly TimeSpan _period;
        private readonly ILogger _logger;

        private TaskCompletionSource<byte> _firstExecutionTaskSource = new TaskCompletionSource<byte>();

        public RecurringTaskService(TimeSpan period, ILogger logger)
        {
            _period = period;
            _logger = logger;
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

        protected abstract Task ExecuteRecurringAsync(CancellationToken ct);

        protected override sealed async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Executing recurring task of service {GetType()}."));
                        await ExecuteAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // Stopped
                    }
                    catch (Exception ex)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Recurring task of service {GetType()} failed.", ex));
                    }
                    finally
                    {
                        if (!_firstExecutionTaskSource.Task.IsCompleted)
                            _firstExecutionTaskSource.SetResult(0);
                    }

                    await Task.Delay(_period, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Fatal error in service", ex));
            }
        }
    }
}
