// [Path]: Pulsar/Pulsar/Services/Tutorial/ITutorialTriggerEngine.cs

using System;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    public interface ITutorialTriggerEngine
    {
        void Setup(TutorialStep step, Action onTriggered);
        void Cleanup();
    }
}
