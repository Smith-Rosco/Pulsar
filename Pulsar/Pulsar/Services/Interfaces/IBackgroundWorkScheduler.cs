using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IBackgroundWorkScheduler
    {
        Task<BackgroundWorkHandle> ScheduleAsync(
            string workId,
            Func<CancellationToken, Task> work,
            BackgroundWorkOptions? options = null);

        void CancelAll();
    }

    public sealed class BackgroundWorkHandle
    {
        public BackgroundWorkHandle(string workId, Task execution)
        {
            WorkId = workId;
            Execution = execution;
        }

        public string WorkId { get; }

        public Task Execution { get; }
    }

    public sealed class BackgroundWorkOptions
    {
        public BackgroundWorkPriority Priority { get; init; } = BackgroundWorkPriority.Normal;

        public BackgroundWorkDuplicateBehavior DuplicateBehavior { get; init; } = BackgroundWorkDuplicateBehavior.StartNew;
    }

    public enum BackgroundWorkPriority
    {
        High,
        Normal,
        Low
    }

    public enum BackgroundWorkDuplicateBehavior
    {
        StartNew,
        ReuseExisting,
        SkipIfRunning
    }
}
