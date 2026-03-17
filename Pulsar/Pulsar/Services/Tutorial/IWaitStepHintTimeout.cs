// [Path]: Pulsar/Pulsar/Services/Tutorial/IWaitStepHintTimeout.cs

using System;
using System.Threading.Tasks;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    public interface IWaitStepHintTimeout
    {
        void Start(TutorialStep step, Func<string?> getCurrentStepId, Func<Task> onTimeoutAsync);
        void Cancel();
    }
}
