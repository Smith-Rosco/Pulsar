## MODIFIED Requirements

### Requirement: GlobalKeyboardHook SHALL implement IModifierStateTracker

The low-level keyboard hook SHALL implement the `IModifierStateTracker` interface so the FocusManager can notify it of synthetic key events.

#### Scenario: OnSyntheticEventBegin suppresses injected modifier updates
- **WHEN** `GlobalKeyboardHook.OnSyntheticEventBegin()` is called
- **THEN** the hook SHALL suppress `UpdateModifierTracker` calls ONLY for injected (synthetic) keyboard events until `OnSyntheticEventEnd()` is called. Physical keyboard events SHALL continue to update the modifier tracker normally.

#### Scenario: OnSyntheticEventEnd restores normal tracking
- **WHEN** `GlobalKeyboardHook.OnSyntheticEventEnd()` is called
- **THEN** the hook SHALL resume normal `UpdateModifierTracker` processing for ALL subsequent hook events (both injected and physical)

#### Scenario: Physical modifier events are never suppressed
- **WHEN** `_syntheticEventSuppression` is true AND a physical (non-injected) modifier KeyUp event arrives
- **THEN** `UpdateModifierTracker` SHALL update the internal tracker state for that modifier, ensuring physical key releases are never lost

#### Scenario: ResetAllModifiers forces all tracked states to released
- **WHEN** `GlobalKeyboardHook.ResetAllModifiers()` is called
- **THEN** all `_trackedCtrlDown`, `_trackedShiftDown`, `_trackedAltDown`, and `_trackedWinDown` SHALL be set to false

### Requirement: FocusManager SHALL NOT use keybd_event for foreground activation

The FocusManager SHALL use `AttachThreadInput` as the primary mechanism for enabling cross-thread foreground activation. The `keybd_event(VK_MENU, KEYEVENTF_KEYUP)` hack SHALL be removed from the activation path.

#### Scenario: Activation does not inject Alt key event
- **WHEN** `IFocusManager.ActivateWindowAsync()` activates a target window
- **THEN** the activation SHALL NOT call `keybd_event(VK_MENU, ...)` at any point

#### Scenario: ForceActivate does not simulate Alt keystrokes
- **WHEN** the `ForceActivate()` fallback method is invoked
- **THEN** it SHALL use only `AllowSetForegroundWindow(-1)` + `SetForegroundWindowNative` + `BringWindowToTop` without injecting any synthetic keystrokes

#### Scenario: Modifier tracker state is not corrupted by activation
- **WHEN** a focus activation completes (whether via `AttachThreadInput` or fallback)
- **THEN** the `GlobalKeyboardHook`'s tracked modifier state SHALL be consistent with the actual physical key state (no phantom modifier changes from activation)

#### Scenario: No Thread.Sleep after synthetic events
- **WHEN** `ForceActivate()` is called
- **THEN** it SHALL NOT call `Thread.Sleep` as a delay for synthetic event propagation, since no synthetic events are injected

## ADDED Requirements

### Requirement: Radial menu SHALL reset modifier state on show and hide

The radial menu SHALL call `IModifierStateTracker.ResetAllModifiers()` when it is shown and when it is hidden to clear any stale modifier state accumulated during menu transitions.

#### Scenario: Modifier state cleared when menu appears
- **WHEN** the radial menu becomes visible
- **THEN** `ResetAllModifiers()` SHALL be called to ensure a clean modifier baseline

#### Scenario: Modifier state cleared when menu hides
- **WHEN** the radial menu is hidden (dismissed)
- **THEN** `ResetAllModifiers()` SHALL be called to clear any stale state before the next hotkey invocation
