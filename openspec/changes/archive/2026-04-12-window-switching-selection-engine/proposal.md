## Why

Pulsar's window switching behavior still depends on partially unified logic: the shared target selection helper and activation path exist, but time semantics, quick-switch state, submenu behavior, and blacklist semantics remain split across `WindowService` and multiple call sites. This makes equivalent switching flows hard to reason about, allows user-visible behavior drift between entry points, and slows down future work on app switching.

## What Changes

- Introduce a dedicated window switching selection engine that centralizes user-facing target selection for process activation, grouped slot switching, submenu default selection, and quick switch resolution.
- Replace ambiguous window recency fields with explicit selection inputs for real activation recency, visual stacking order, and stable display ordering.
- Separate window inventory, window tracking, selection, and activation concerns so `WindowService` no longer owns every switching responsibility.
- Define and document submenu ordering semantics, quick-switch state semantics, and blacklist behavior so plugin actions and radial menu flows remain consistent.
- Expand decision-focused tests around selection and quick-switch behavior so future refactors can change structure without changing user-visible outcomes.

## Capabilities

### New Capabilities
- `window-switching-architecture`: Defines the service boundaries and decision model for inventory, tracking, selection, quick-switch state, and activation in the window switching subsystem.
- `window-switching-behavior-contract`: Defines the user-visible behavioral contract for process switching, grouped switching, submenu defaults, quick switch, and blacklist handling.

### Modified Capabilities
- `window-switch-selection-core`: Extend the shared selection contract so selection contexts express switching intent explicitly rather than only a skip mode, and so selection consumes explicit activation, Z-order, and display-order semantics.
- `window-switch-activation-path`: Clarify that all foreground switching flows, including quick switch and focus restore paths where applicable, must use the shared activation path.
- `plugin-action-semantics`: Clarify WinSwitcher blacklist behavior and preserve `activate` / `switch` / `launch` intent semantics while the runtime moves to the new selection engine.

## Impact

- Affected code: `Pulsar/Pulsar/Services/WindowService.cs`, `Services/Interfaces/IWindowService.cs`, window tracking helpers, radial menu coordinators and strategies, `WinSwitcherPlugin`, and related tests.
- New code likely includes dedicated services or components for window inventory, tracking, selection, and quick-switch state.
- Documentation updates are required for WinSwitcher behavior, submenu semantics, and refactoring guidance.
- No external dependency changes are expected; this is primarily an internal architecture and behavior-contract refactor.
