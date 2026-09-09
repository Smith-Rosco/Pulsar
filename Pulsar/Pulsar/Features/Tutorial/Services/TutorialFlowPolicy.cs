// [Path]: Pulsar/Pulsar/Features/Tutorial/Services/TutorialFlowPolicy.cs

using System.Collections.Generic;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Views;

namespace Pulsar.Features.Tutorial.Services
{
    /// <summary>
    /// Pure decision logic for tutorial flow advancement. Extracted from
    /// TutorialOrchestrator (architecture review candidate C5, 2026-09-09) so the
    /// skip/focus rules are unit-testable without WPF, dispatchers, or the overlay
    /// window stack. No state: every method is a total function of its inputs.
    /// </summary>
    public static class TutorialFlowPolicy
    {
        /// <summary>
        /// The switch-mode success confirmation step ("step3_switch_mode_success")
        /// is auto-skipped when the user advances via a real trigger: the toast
        /// notification replaces the confirmation card. Pure form of the
        /// Orchestrator's Step 2→3 optimization.
        /// </summary>
        public static bool ShouldSkipConfirmationStep(IReadOnlyList<TutorialStep> steps, int stepIndex)
        {
            if (steps == null || stepIndex < 0 || stepIndex >= steps.Count)
                return false;

            var step = steps[stepIndex];
            return step.Id == "step3_switch_mode_success" && step.Type == TutorialStepType.Instruction;
        }

        /// <summary>
        /// Initial overlay state for a step: AlwaysFocused/AlwaysObserving are
        /// explicit; Auto means Instruction steps focus the overlay while
        /// WaitForAction steps leave it observing so the user can interact with
        /// the app. Unknown enum values fall back to Focused (history behaviour).
        /// </summary>
        public static OverlayState DetermineFocusState(TutorialStep step)
        {
            switch (step.FocusMode)
            {
                case TutorialFocusMode.AlwaysFocused:
                    return OverlayState.Focused;

                case TutorialFocusMode.AlwaysObserving:
                    return OverlayState.Observing;

                case TutorialFocusMode.Auto:
                    return step.Type == TutorialStepType.Instruction
                        ? OverlayState.Focused
                        : OverlayState.Observing;

                default:
                    return OverlayState.Focused;
            }
        }
    }
}
