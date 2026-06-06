# keyboard-hook-focus-sync Specification

## Purpose
TBD - created by archiving change unified-focus-management. Update Purpose after archive.
## Requirements
### Requirement: FocusManager SHALL coordinate synthetic key events with the keyboard hook
When `IFocusManager` performs operations that inject synthetic keyboard events into the system input stream, it SHALL notify the registered `IModifierStateTracker` before and after the injection so the keyboard hook can protect its modifier state from corruption.

#### Scenario: Synthetic event notification before activation
- **WHEN** `IFocusManager` is about to inject a synthetic keyboard event as part of a focus activation operation
- **THEN** it SHALL call `IModifierStateTracker.OnSyntheticEventBegin()` before the injection and `IModifierStateTracker.OnSyntheticEventEnd()` after

#### Scenario: No tracker registered
- **WHEN** `IFocusManager` performs a focus operation but no `IModifierStateTracker` has been registered
- **THEN** the operation SHALL proceed normally without notification calls (tracker is optional)

### Requirement: GlobalKeyboardHook SHALL implement IModifierStateTracker
The low-level keyboard hook SHALL implement the `IModifierStateTracker` interface so the FocusManager can notify it of synthetic key events.

#### Scenario: OnSyntheticEventBegin suppresses modifier updates
- **WHEN** `GlobalKeyboardHook.OnSyntheticEventBegin()` is called
- **THEN** the hook SHALL temporarily suppress `UpdateModifierTracker` calls for subsequent hook events until `OnSyntheticEventEnd()` is called

#### Scenario: OnSyntheticEventEnd restores normal tracking
- **WHEN** `GlobalKeyboardHook.OnSyntheticEventEnd()` is called
- **THEN** the hook SHALL resume normal `UpdateModifierTracker` processing for all subsequent hook events

#### Scenario: ResetAllModifiers forces all tracked states to released
- **WHEN** `GlobalKeyboardHook.ResetAllModifiers()` is called
- **THEN** all `_trackedCtrlDown`, `_trackedShiftDown`, `_trackedAltDown`, and `_trackedWinDown` SHALL be set to false

### Requirement: FocusManager SHALL NOT use keybd_event for foreground activation
The FocusManager SHALL use `AttachThreadInput` as the primary mechanism for enabling cross-thread foreground activation. The `keybd_event(VK_MENU, KEYEVENTF_KEYUP)` hack SHALL be removed from the activation path.

#### Scenario: Activation does not inject Alt key event
- **WHEN** `IFocusManager.ActivateWindowAsync()` activates a target window
- **THEN** the activation SHALL NOT call `keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0)` at any point

#### Scenario: Modifier tracker state is not corrupted by activation
- **WHEN** a focus activation completes (whether via `AttachThreadInput` or fallback)
- **THEN** the `GlobalKeyboardHook`'s tracked modifier state SHALL be consistent with the actual physical key state (no phantom Alt-release from activation)

