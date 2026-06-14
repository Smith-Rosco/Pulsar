## Why

The current tutorial onboarding is failing new users: its wording is robotic and impersonal, the Next button stays visible on action-based steps (making it trivially skippable), step transitions flicker the overlay window, and the content doesn't demonstrate Pulsar's real value (switching to an app then running a command on it). Users complete the tutorial without actually learning the product, and there is no celebration or sense of achievement at the end. First-run retention depends on fixing this.

## What Changes

- Rewrite all tutorial content (English + Chinese) with warm, encouraging, human tone — replace system-description with user-experience language
- Replace abstract "choose any slot" steps with a concrete, memorable scenario: switch to Notepad → run command to insert text into Notepad
- Fix WaitForAction steps: hide the Next button until 30s timeout, forcing users to actually perform the action
- Eliminate overlay window flicker between steps — switch card content in-place with smooth crossfade animation instead of cleanup+re-show
- Add confetti celebration animation on tutorial completion step
- Add "Restart Tutorial" entry point in Settings (so users can replay)
- Add step-success visual feedback (checkmark / pulse on the card when a trigger fires)

## Capabilities

### New Capabilities
- `completion-celebration`: Confetti/fireworks particle animation that plays when the user finishes the last tutorial step, providing a sense of accomplishment

### Modified Capabilities
- `guided-onboarding-tutorial`: Tutorial step content, flow sequence, wait-for-action behavior, next-button visibility rules, step transition visual continuity, and success feedback

## Impact

- `Pulsar/Pulsar/Assets/TutorialSteps.json` — step definitions rewritten
- `Pulsar/Pulsar/Resources/Strings.resx` — all tutorial text replaced
- `Pulsar/Pulsar/Resources/Strings.zh-CN.resx` — all Chinese tutorial text replaced
- `Pulsar/Pulsar/Services/Tutorial/TutorialOrchestrator.cs` — transition logic, step flow behavior
- `Pulsar/Pulsar/Services/Tutorial/OverlayManager.cs` — possibly new methods for smooth transition
- `Pulsar/Pulsar/Views/Tutorial/TutorialOverlayWindow.xaml` / `.cs` — crossfade transition, confetti rendering layer
- `Pulsar/Pulsar/Views/Tutorial/TutorialStepCard.xaml` / `.cs` — Next button visibility logic, success state UI, confetti trigger
- New file: confetti animation control (custom `DrawingVisual`-based particle system or lightweight library)
- `Pulsar/Pulsar/Helpers/Tutorial/` — possible new helper for confetti
- `Pulsar/Pulsar/ViewModels/` or Settings views — "Restart Tutorial" entry point
