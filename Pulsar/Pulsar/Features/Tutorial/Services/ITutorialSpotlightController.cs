// [Path]: Pulsar/Pulsar/Services/Tutorial/ITutorialSpotlightController.cs

using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Services
{
    public interface ITutorialSpotlightController
    {
        void ApplyForStep(TutorialStep step);
        void RefreshIfFocused(TutorialStep step);
    }
}
