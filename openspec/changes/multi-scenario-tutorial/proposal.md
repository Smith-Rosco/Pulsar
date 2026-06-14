## Why

The current tutorial system has a single 6-step flow (Notepad + sendkeys) that demonstrates only the CommandPlugin. To showcase Pulsar's full plugin ecosystem (VbaRunner, BookmarkletRunner, CommandPlugin), the tutorial must support multiple scenarios — each tailored to a different use case and its prerequisite software. Additionally, the step 2→3 transition has a context-switching friction point that reduces completion rates.

## What Changes

- **TutorialScenario model**: Replace the flat `OnboardingUsageProfile` enum with a rich class that bundles slot templates, prerequisite checks, and scenario-specific step definitions
- **Prerequisite validation system**: New `IPrerequisiteChecker` interface + built-in checkers (Excel, VBA, Browser) that report status in real-time on the setup wizard
- **Scenario-specific TutorialSteps JSON**: Per-scenario step definition files (e.g., `TutorialSteps.excel.json`) sharing the 6-step structure but with different copy and target slot references
- **Multi-slot profile generation**: `BuildInitialConfig()` generates N command slots per scenario (primary tutorial slot + optional extras), not just 1
- **Step 2 fallback**: When the target slot doesn't exist (e.g., user skipped setup wizard), the tutorial shows a guidance card leading to Settings instead of hanging forever on `WaitForAction`
- **Step 2→3 transition improvement**: After switch action succeeds, auto-proceed to step 4 (command mode) with an optional inline prompt, eliminating the forced "Click Next" intermediary step
- **Prerequisite-aware scenario cards**: Setup wizard displays per-scenario software requirements with live detection status (✅/⚠️/🛑)
- **Browser scenario** (P1): Bookmarklet runner demo with browser detection

## Capabilities

### New Capabilities
- `scenario-core`: TutorialScenario data model, TutorialScenarioRegistry, step loader routing, slot template generation
- `scenario-prerequisite-validation`: IPrerequisiteChecker interface, built-in checkers (Excel, VBA, Browser), UI status rendering
- `scenario-excel-vba`: Excel scenario definition, VbaRunner demo slot, Excel detection, fallback to sendkeys when VBA unavailable

### Modified Capabilities
- `guided-onboarding-tutorial`: Step definitions become scenario-aware (load per-scenario JSON); step 2→3 transition redesigned; step 2 slot-missing fallback added
- `first-launch-setup-wizard`: Scenario cards now show prerequisite status; Finish button respects Required-status checks; scenario selection affects which command slots are generated

## Impact

- **Models**: New `TutorialScenario.cs` class; `OnboardingUsageProfile` enum remains for backward compat but is wrapped by the new model
- **Services**: New `TutorialScenarioRegistry` (singleton), new `PrerequisiteService`, new per-checker files in `Services/Tutorial/Prerequisites/`
- **ViewModels**: `FirstLaunchSetupWizardViewModel` gains scenario prerequisite tracking, updated validation logic
- **Views**: `FirstLaunchSetupWizardContent.xaml` adds prerequisite status indicators per card
- **Assets**: New `TutorialSteps.excel.json` and `TutorialSteps.browser.json`; existing `TutorialSteps.json` unchanged
- **Plugins**: No plugin changes needed — all scenarios use existing plugins (CommandPlugin, VbaRunner, BookmarkletRunner, WinSwitcher)
- **Localization**: ~40 new resource entries for scenario titles, descriptions, prerequisite labels
