## Why

The WinSwitcher subsystem has a mature underlying architecture (three-tier fallback selection, shared activation path, DWM live preview, layered action feedback), but several user-facing perception gaps remain. Users experience silent failures when UIPI blocks `SetForegroundWindow`, no feedback during process launch fallback, poor sub-radial window identifiability when a process has many windows, and no preference for same-monitor targets. Closing these gaps requires minimal new infrastructure — most changes piggyback on existing abstractions.

## What Changes

- **FlashWindowEx post-activation confirmation**: The shared `WindowActivator` calls `FlashWindowEx` after a successful `SetForegroundWindow`, giving users unambiguous visual confirmation even when UIPI prevents the window from visibly foregrounding.
- **Launching toast during SmartSwitch fallback**: When `SmartSwitchAsync` falls through to `Process.Start()` because no running window was found, the system shows a "Launching [app]..." tray notification so the user knows startup is in progress rather than staring at a dismissed radial menu.
- **Sub-radial window title on slots**: Sub-menu slots representing individual windows of a grouped process show the window title as the slot label instead of just a numbered suffix ("Chrome (1)" → "inbox – Gmail").
- **Same-monitor preference in window selection**: When multiple candidate windows are equivalent in activation recency, `WindowSelectionEngine` prefers windows on the same monitor as the cursor (where the radial menu appeared).

## Capabilities

### New Capabilities
- `window-activation-confirmation`: FlashWindowEx visual confirmation after window activation.
- `smart-switch-launch-feedback`: User-facing toast during process launch fallback path.
- `sub-radial-window-title`: Window title displayed as slot label for individual window sub-menu slots.

### Modified Capabilities
- `window-switch-selection-core`: Same-monitor preference ordering added to target-window selection criteria.
- `user-facing-action-feedback`: New "Launching" feedback kind for in-progress process startup.
- `window-switch-activation-path`: FlashWindowEx added as post-activation step in shared activation path.

## Impact

- **Affected code**: `WindowActivator.cs` (+15 lines FlashWindowEx call), `PulsarNative.cs` (+20 lines FLASHWINFO struct + P/Invoke), `WindowSelectionEngine.cs` (+30 lines same-monitor preference), `ActionFeedbackService.cs` (+10 lines Launching kind/routing), `WinSwitcherPlugin.cs` (+5 lines toast on fallback), `RadialMenuViewModel.cs` (+15 lines window title for sub-slots)
- **No breaking changes**: All additions are opt-in or additive. Existing activation, feedback, and selection behavior is preserved.
- **Dependencies**: None. All work is within existing services and Native layer.
