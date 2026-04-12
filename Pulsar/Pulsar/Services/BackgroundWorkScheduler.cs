using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public sealed class BackgroundWorkScheduler : IBackgroundWorkScheduler, IDisposable
    {
        private readonly ILogger<BackgroundWorkScheduler> _logger;
        private readonly ConcurrentDictionary<string, ScheduledWork> _runningWork = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource _shutdown = new();

        public BackgroundWorkScheduler(ILogger<BackgroundWorkScheduler> logger)
        {
            _logger = logger;
        }

        public Task<BackgroundWorkHandle> ScheduleAsync(
            string workId,
            Func<CancellationToken, Task> work,
            BackgroundWorkOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workId);
            ArgumentNullException.ThrowIfNull(work);

            options ??= new BackgroundWorkOptions();

            if (_runningWork.TryGetValue(workId, out var existing) && !existing.Execution.IsCompleted)
            {
                switch (options.DuplicateBehavior)
                {
                    case BackgroundWorkDuplicateBehavior.ReuseExisting:
                        _logger.LogDebug("[BackgroundWork] Reusing in-flight work item {WorkId} ({Priority})", workId, options.Priority);
                        return Task.FromResult(new BackgroundWorkHandle(workId, existing.Execution));

                    case BackgroundWorkDuplicateBehavior.SkipIfRunning:
                        _logger.LogDebug("[BackgroundWork] Skipping duplicate work item {WorkId} ({Priority})", workId, options.Priority);
                        return Task.FromResult(new BackgroundWorkHandle(workId, Task.CompletedTask));
                }
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            var execution = Task.Run(async () =>
            {
                try
                {
                    if (options.Priority == BackgroundWorkPriority.Low)
                    {
                        await Task.Yield();
                    }

                    _logger.LogInformation("[BackgroundWork] Starting {WorkId} ({Priority})", workId, options.Priority);
                    await work(linkedCts.Token);
                    _logger.LogInformation("[BackgroundWork] Completed {WorkId}", workId);
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
                {
                    _logger.LogInformation("[BackgroundWork] Cancelled {WorkId}", workId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BackgroundWork] Work item failed: {WorkId}", workId);
                }
                finally
                {
                    linkedCts.Dispose();
                    _runningWork.TryRemove(workId, out _);
                }
            }, CancellationToken.None);

            _runningWork[workId] = new ScheduledWork(execution, linkedCts);
            return Task.FromResult(new BackgroundWorkHandle(workId, execution));
        }

        public void CancelAll()
        {
            if (_shutdown.IsCancellationRequested)
            {
                return;
            }

            _logger.LogInformation("[BackgroundWork] Cancelling {Count} scheduled work item(s)", _runningWork.Count);
            _shutdown.Cancel();
        }

        public void Dispose()
        {
            CancelAll();
            _shutdown.Dispose();
        }

        private sealed class ScheduledWork
        {
            public ScheduledWork(Task execution, CancellationTokenSource cancellation)
            {
                Execution = execution;
                Cancellation = cancellation;
            }

            public Task Execution { get; }

            public CancellationTokenSource Cancellation { get; }
        }
    }
}
