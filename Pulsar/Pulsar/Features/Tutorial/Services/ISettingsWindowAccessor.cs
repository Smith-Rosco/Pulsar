// [Path]: Pulsar/Pulsar/Services/Tutorial/ISettingsWindowAccessor.cs

using Wpf.Ui.Controls;

namespace Pulsar.Features.Tutorial.Services
{
    public interface ISettingsWindowAccessor
    {
        NavigationView? TryGetNavigationView();

        /// <summary>
        /// Activates an already-visible Settings window, or creates and shows one
        /// through the composition root's <c>Func&lt;SettingsWindow&gt;</c> factory.
        /// Extracted from TutorialOrchestrator (C5, 2026-09-09) so the Orchestrator
        /// no longer reaches into <c>Application.Current</c> for the DI container.
        /// Returns false when the window could not be surfaced.
        /// </summary>
        bool TryOpenOrActivateSettingsWindow();
    }
}
