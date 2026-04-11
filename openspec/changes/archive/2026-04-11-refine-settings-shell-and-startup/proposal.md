## Why

Pulsar's settings experience and startup flow have accumulated substantial capability, but the application shell around them is still relatively monolithic. The settings window mixes navigation, state orchestration, and configuration editing in a single large ViewModel, while startup performs many responsibilities synchronously in `App.xaml.cs`, making future iteration riskier and performance tuning harder.

This change is needed now to create a cleaner foundation for continued product growth. It will make the settings experience easier to evolve, separate local UI preferences from core business configuration, and introduce a staged startup model that preserves correctness while reducing perceived startup cost.

## What Changes

- Introduce a dedicated settings shell architecture that separates page navigation/state from configuration editing responsibilities.
- Add a local UI preferences capability for lightweight, device-local settings such as last-opened page, window state, and other non-business preferences.
- Introduce a staged startup coordination model that distinguishes blocking startup work from deferred warm-up tasks.
- Define clear responsibility boundaries between shell/navigation services, configuration services, and startup orchestration.
- Preserve existing plugin behavior, configuration semantics, and published runtime contracts while improving internal structure.

## Capabilities

### New Capabilities
- `settings-shell-navigation`: A dedicated settings shell that owns page registration, page navigation state, and shell-level interactions independently from configuration editing logic.
- `local-ui-preferences`: A lightweight storage capability for local UI-only preferences that should not live in `Profiles.json`.
- `staged-startup-coordination`: A startup orchestration model that classifies initialization work into blocking and deferred phases while preserving required readiness guarantees.

### Modified Capabilities
- `settings-dirty-state-guard`: Clarify how unsaved-change protection behaves when shell navigation is separated from configuration editing and when the last-opened settings page can be restored.

## Impact

**Affected Code:**
- `Pulsar/Pulsar/ViewModels/SettingsViewModel.cs`
- `Pulsar/Pulsar/Views/SettingsWindow.xaml`
- `Pulsar/Pulsar/App.xaml.cs`
- Settings-related pages, services, and dialog coordination under `Pulsar/Pulsar/Views/Pages/`, `Pulsar/Pulsar/ViewModels/`, and `Pulsar/Pulsar/Services/`

**Affected Systems:**
- Settings shell and navigation
- Local settings persistence
- Startup and initialization flow
- Unsaved changes protection

**Dependencies / Constraints:**
- Must preserve `Profiles.json` as the source of truth for business configuration.
- Must respect current WPF theme injection and button style rules documented in `AGENTS.md`.
- Must avoid regressing plugin load correctness, tray initialization, hotkey readiness, and tutorial startup behavior.
