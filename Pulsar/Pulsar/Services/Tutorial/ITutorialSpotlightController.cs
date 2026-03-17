// [Path]: Pulsar/Pulsar/Services/Tutorial/ITutorialSpotlightController.cs

using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    public interface ITutorialSpotlightController
    {
        void ApplyForStep(TutorialStep step);
        void RefreshIfFocused(TutorialStep step);
    }
}
