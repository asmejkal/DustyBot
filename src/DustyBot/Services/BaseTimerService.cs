using Discord;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    internal abstract class BaseRecurringTaskService : IService, IDisposable
    {
        private readonly TimeSpan _period;
        private readonly ILogger _logger;

        private TaskCompletionSource<byte> _firstExecutionTaskSource = new TaskCompletionSource<byte>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _executeTask;

        public BaseRecurringTaskService(TimeSpan period, ILogger logger)
        {
            _period = period;
            _logger = logger;
        }

        public Task StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _executeTask = Task.Run(() => RunAsync(_cancellationTokenSource.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_cancellationTokenSource != null && _cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                await _executeTask;

                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;

                if (!_firstExecutionTaskSource.Task.IsCompleted)
                    _firstExecutionTaskSource.SetCanceled();
            }
        }

        protected abstract Task ExecuteAsync(CancellationToken ct);

        protected async Task WaitForFirstCompletion()
        {
            await _firstExecutionTaskSource.Task;
        }

        private async Task RunAsync(CancellationToken ct)
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

        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    ((IDisposable)_cancellationTokenSource)?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
