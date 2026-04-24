## Why

Grouped process slots in the radial menu already support direct execution, but their default target-selection behavior is optimized for generic grouped switching rather than the specific intent of releasing the modifier over a root-menu slot. That makes direct trigger behavior feel inconsistent when the user expects the root slot to act like a fast return-to-app action from other apps, while still rotating to another window when already inside the same app.

## What Changes

- Refine root radial-menu direct execution for grouped process slots so modifier-release execution uses a dedicated target-selection intent instead of the generic grouped-switch behavior.
- Define the direct-trigger rule as: switch to the process's most recently used window when the current foreground window is outside the target process; otherwise skip the current in-process window and switch to the next most recently used eligible window.
- Preserve existing click-to-drill-down behavior for grouped slots so explicit window choice remains available without changing mouse navigation.
- Clarify how grouped-slot direct execution relates to submenu ordering, so stable submenu display order remains independent from the default switch target.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `radial-menu`: change grouped root-slot modifier-release behavior so direct execution uses a dedicated default target rule without changing left-click drill-down interaction.
- `window-switch-selection-core`: extend the shared target-window selection contract with a root grouped-slot direct-trigger intent that prefers MRU return-to-app behavior and skips the current in-process window when appropriate.

## Impact

- Affected UI behavior: root radial-menu execution for grouped process slots.
- Affected switching logic: `ProcessGroupStrategy`, `RadialMenuInputCoordinator`, and shared window-selection intent handling.
- Affected tests: window-selection core tests and radial/grouped slot behavior tests.
- No external API or dependency changes are expected.
