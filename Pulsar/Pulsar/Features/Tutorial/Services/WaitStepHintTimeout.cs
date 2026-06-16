// [Path]: Pulsar/Pulsar/Services/Tutorial/WaitStepHintTimeout.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Services
{
    public class WaitStepHintTimeout : IWaitStepHintTimeout
    {
        private readonly ILogger<WaitStepHintTimeout> _logger;
        private readonly TimeSpan _timeout;

        private CancellationTokenSource? _cts;

        public WaitStepHintTimeout(ILogger<WaitStepHintTimeout> logger, TimeSpan? timeout = null)
            : this(logger, timeout ?? TimeSpan.FromSeconds(30))
        {
        }

        internal WaitStepHintTimeout(ILogger<WaitStepHintTimeout> logger, TimeSpan timeout)
        {
            _logger = logger;
            _timeout = timeout;
        }

        public void Start(TutorialStep step, Func<string?> getCurrentStepId, Func<Task> onTimeoutAsync)
        {
            if (step.Type != TutorialStepType.WaitForAction && step.Type != TutorialStepType.WaitForNavigation)
            {
                return;
            }

            Cancel();

            var stepId = step.Id;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_timeout, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (getCurrentStepId() != stepId)
                {
                    return;
                }

                try
                {
                    await onTimeoutAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WaitStepHintTimeout] Timeout callback failed");
                }
            });
        }

        public void Cancel()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            _cts = null;
        }
    }
}
