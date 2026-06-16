// [Path]: Pulsar/Pulsar/Services/Tutorial/IWaitStepHintTimeout.cs

using System;
using System.Threading.Tasks;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Services
{
    public interface IWaitStepHintTimeout
    {
        void Start(TutorialStep step, Func<string?> getCurrentStepId, Func<Task> onTimeoutAsync);
        void Cancel();
    }
}
