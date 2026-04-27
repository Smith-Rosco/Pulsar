## Why

The WinSwitcher process blacklist dialog currently performs heavyweight window discovery, icon loading, and registry side effects during initial display, which can make the settings UI appear hung when many processes are known or icon extraction is slow. At the same time, the runtime contract around `ExcludeProcesses` has drifted, so discovery-only blacklist entries can affect explicit activation paths contrary to the documented behavior.

## What Changes

- Make the WinSwitcher process blacklist dialog load in a UI-safe way that avoids long synchronous work on the foreground thread.
- Separate lightweight running-state lookup for the blacklist UI from full window inventory and process-registration work.
- Remove query-time side effects from blacklist-related discovery flows so opening configuration surfaces does not trigger unrelated registry mutations or cache work.
- Re-align WinSwitcher blacklist behavior so discovery filtering does not block explicit process activation unless a separate activation-denial rule is defined.
- Add coverage for large registries, slow icon sources, and explicit activation of discovery-blacklisted processes.

## Capabilities

### New Capabilities
- `winswitcher-blacklist-dialog-performance`: Defines the responsiveness and loading behavior contract for the WinSwitcher process blacklist configuration experience.

### Modified Capabilities
- `window-switching-behavior-contract`: Clarify and enforce that discovery blacklist entries do not deny explicit activation unless an activation denylist is explicitly defined.
- `background-work-scheduling`: Require configuration-facing discovery flows to avoid ad hoc side-effecting background work during foreground UI loads.

## Impact

- Affected code: `Pulsar/Pulsar/ViewModels/Dialogs/ProcessBlacklistViewModel.cs`, `Pulsar/Pulsar/ViewModels/Settings/PluginViewModel.cs`, `Pulsar/Pulsar/Services/WindowService.cs`, `Pulsar/Pulsar/Services/WindowSwitching/WindowInventoryService.cs`, `Pulsar/Pulsar/Services/ProcessRegistryService.cs`, related dialog XAML, and WinSwitcher tests.
- Affected behavior: WinSwitcher blacklist settings UX, process running-state lookup, inventory/registration boundaries, and explicit window switching semantics.
- Dependencies: existing window-switching services, process registry/icon cache, and settings dialog infrastructure.
