## Why

Pulsar's window switching behavior currently works through multiple code paths that consume the same tracking data with different selection rules. This creates inconsistent app-switching behavior between WinSwitcher plugin execution, grouped radial slots, and Quick Switch, while also concentrating too many responsibilities inside `WindowService`.

The highest-value improvements are now architectural rather than additive. We need a phased refactor that first unifies window target selection and activation behavior, then progressively reduces the maintenance risk in the switching stack without attempting a full rewrite in one pass.

## What Changes

1. Introduce a dedicated window selection capability that defines a single decision model for choosing the target window across WinSwitcher plugin actions and grouped radial-menu switching.
2. Standardize window recency semantics so switching flows stop mixing synthetic Z-order timestamps with real activation history when making user-facing selection decisions.
3. Introduce a unified window activation path so foreground switching, minimized-window restore behavior, and switch result handling are not split between `WindowService` and UI strategies.
4. Limit the initial implementation scope to the highest-return core refactors and explicitly defer larger follow-up work, such as full `WindowService` decomposition and Quick Switch extraction, until the unified selection path is stable.

## Capabilities

### New Capabilities
- `window-switch-selection-core`: A shared selection contract for choosing target windows across app-switching entry points using consistent recency and skip rules.
- `window-switch-activation-path`: A unified activation contract for bringing a selected window to the foreground, including minimized-window restore handling.

### Modified Capabilities
- `plugin-action-semantics`: App switching actions must preserve their documented switch-only, launch-only, and switch-or-launch semantics while adopting the shared window selection core.

## Impact

**Affected Code:**
- `Pulsar/Pulsar/Services/WindowService.cs`
- `Pulsar/Pulsar/Services/Interfaces/IWindowService.cs`
- `Pulsar/Pulsar/Plugins/Core/WinSwitcher/WinSwitcherPlugin.cs`
- `Pulsar/Pulsar/ViewModels/Strategies/SlotStrategies.cs`
- `Pulsar/Pulsar/ViewModels/Strategies/ProcessPageProvider.cs`
- `Pulsar/Pulsar/ViewModels/RadialMenuSubMenuCoordinator.cs`
- `Pulsar/Pulsar/Models/ProcessWindowInfo.cs` or related selection models if recency semantics need to be clarified in shared types

**Potential Tests:**
- Window selection decision tests for multi-window processes
- Activation-path tests for minimized and already-foreground windows
- Regression tests for WinSwitcher action behavior and grouped slot switching

**Deferred / Out of Scope for This Change:**
- Full extraction of Quick Switch into a dedicated engine
- Complete decomposition of `WindowService` into multiple services
- UI redesign of submenu ordering beyond the minimum changes needed to align selection behavior

**No intended breaking change to user-facing plugin actions**; the goal is to make existing switching behaviors more consistent and predictable.
