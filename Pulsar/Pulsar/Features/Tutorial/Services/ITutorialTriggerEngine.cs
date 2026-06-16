// [Path]: Pulsar/Pulsar/Services/Tutorial/ITutorialTriggerEngine.cs

using System;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Services
{
    public interface ITutorialTriggerEngine
    {
        void Setup(TutorialStep step, Action onTriggered);
        void Cleanup();
    }
}
