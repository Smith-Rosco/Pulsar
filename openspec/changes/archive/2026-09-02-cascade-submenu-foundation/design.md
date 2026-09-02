## Context

See proposal.md — Why. Current constraints that shape the approach:

- `MenuSession` is the single state machine; `RadialMenuViewModel` is a thin projection. All submenu transitions (`EnterSubMenuAsyncCore` / `RestoreRootMenuAsync`) and the `_subMenuCoordinator` live in `MenuSession` (`MenuSession.cs:1833`, `:2004`).
- `IMenuSession.EnterSubMenuAsync` is the only breakable surface (`IMenuSession.cs:18`); `ProcessGroupStrategy.EnterSubMenuAsync` (`SlotStrategies.cs:310`) and `RadialMenuViewModel.EnterSubMenuAsync` (`RadialMenuViewModel.cs:798`) both forward into it.
- `RadialMenuSubMenuCoordinator` is 100% window-specific: `ConfigureSubMenu(List<ProcessWindowInfo>, ...)` hardcodes `BackActionStrategy` center + `WindowSwitchStrategy`/`NoOpStrategy` children, thumbnail capture, `SubMenuColorPalette`, and `_windowService.SelectTargetWindow` (submenu intent).
- Submenu pagination is computed in `MenuSession` from `_subMenuWindows.Count / _slotsPerPage`; the coordinator consumes `slotsPerPage` + `pageIndex`.
- `ProcessWindowInfo` is bound to the click path in `HandleGlobalMouseClickAsync` (`MenuSession.cs:919`) which special-cases `ProcessGroupStrategy` + `List<ProcessWindowInfo>` to drill in.
- `PluginSlot` is JSON-persisted (`ProfilesConfig.cs:537`); `CommandMode`/`SwitchMode` use `LegacySlotConverter`. `SlotViewModel` has `DataContext`/`Type` but no sub-slot tree.
- Existing window-submenu specs (submenu-expansion-animation, submenu-slot-thumbnail, submenu-window-color-coding) define the window path behavior that MUST be preserved verbatim.

## Goals / Non-Goals

**Goals:**
- Introduce a strategy-selected submenu coordinator where window switching is one concrete strategy.
- Generalize the submenu entry contract from `List<ProcessWindowInfo>` to a descriptor while keeping the window path behavior byte-identical.
- Add a `SubSlots` data model on `SlotViewModel` + optional persisted sub-actions on `PluginSlot`, so future cascade forms can be authored without re-touching the session.
- Land a clean seam for Change B (layout) and Change C (editor) without speculative geometry.

**Non-Goals:**
- Ring/Fan sub-layout geometry — Change B.
- Second-level action editor UI / smart default injection — Change C.
- Any new submenu strategy beyond window switching; the descriptor routing must gracefully handle unknown strategies but no second strategy ships here.
- Changing the shared window-selection contract (`window-switch-selection-core`) — delegated unchanged via `WindowSelectionRequest`.

## Decisions

### D1 — Descriptor-first submenu entry contract (**BREAKING**)
Replace `IMenuSession.EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName, int clickedSlotIndex)` with `EnterSubMenuAsync(SubMenuDescriptor descriptor, int clickedSlotIndex)`.

- `SubMenuDescriptor` is an abstract base; concrete payloads: `WindowSubMenuDescriptor` (processName, `IReadOnlyList<ProcessWindowInfo>` windows) and a placeholder `CascadeSubMenuDescriptor` (source slot's `SubSlots`), reserved for Change B.
- `ProcessGroupStrategy.EnterSubMenuAsync` builds a `WindowSubMenuDescriptor`; `HandleGlobalMouseClickAsync` drills in via descriptor construction instead of type-checking `ProcessGroupStrategy`.
- Rationale: the contract is the invariant everything else funnels through. Change B/C then only need a new descriptor payload + a new strategy.
- Alternatives considered: (a) keep the window overload and add a parallel method — rejected, two entry paths invite drift; (b) pass the whole `SlotViewModel` — rejected, leaks presentation state into the contract.

### D2 — Strategy registry with DI resolution
Introduce `ISubMenuStrategy` with a `StrategyId` and a `ConfigureSubMenu(SubMenuContext ctx, SubMenuDescriptor descriptor)` method. `RadialMenuSubMenuCoordinator` becomes a thin host that resolves `IEnumerable<ISubMenuStrategy>` (registered in `App.xaml.cs`), selects by `descriptor.StrategyId`, and delegates.

- `WindowSwitchSubMenuStrategy` encapsulates today's `ConfigureSubMenu` body 1:1 (center `BackActionStrategy`, child `WindowSwitchStrategy`/`NoOpStrategy`, thumbnail, palette, `SelectTargetWindow` submenu intent, logging).
- Unknown strategy id → log warning + fall back to root menu (spec: `submenu-coordinator-strategy`).
- Rationale: DI registration mirrors `StyleRendererFactory` (already the project pattern for pluggable renderer selection); avoids a hand-rolled switch.
- Alternatives: a `switch` in the coordinator — rejected, that is the coupling we are removing.

### D3 — `SubSlots` on SlotViewModel + optional persisted sub-actions on PluginSlot
`SlotViewModel` gains `ObservableCollection<SubSlotDescriptor> SubSlots` (always present, empty by default). `PluginSlot` gains an optional `SubActions` list (`List<SubSlotDescriptor>`? null when absent) serialized under a new camelCase JSON key; missing key deserializes to null → empty collection.

- No migration: property is optional, deserialization tolerant. `ConfigService`/`LegacySlotConverter` untouched unless a round-trip test exposes a need.
- `SubSlotDescriptor` is a light record: `PluginId`, `Action`, `Args` (dict), `Label`, `IconKey`, `ColorHex` — deliberately mirroring `PluginSlot`'s own fields so Change C's editor can reuse `SlotEditorViewModel`-style editing.
- Rationale: keeps persistence orthogonal to runtime strategy; window drill-in stays runtime-computed (`List<ProcessWindowInfo>`) and needs no config field.
- Alternatives: model sub-actions as nested `PluginSlot` — rejected, `PluginSlot` carries runtime slots/paging semantics inappropriate for a child descriptor.

### D4 — Coordinator owns pagination; session drives transition only
The coordinator (via strategy) computes page count from the descriptor payload; `MenuSession` keeps the morph/cancel logic as-is but stops computing `_subMenuWindows`/`_subMenuProcessName` itself. Session state (`_subMenuWindows`, `_subMenuPage`, `_subMenuTotalPages`) moves into the coordinator's submenu context or is read back from the strategy's configured state.

- Restore path: `RestoreRootMenuAsync` delegates clearing to the coordinator's `RestoreRootMenu(...)` (already exists) — unchanged contract.
- Rationale: pagination is a "what to render" concern; keeping it in the session couples the state machine to every future strategy's data shape.
- Trade-off: the session must query the coordinator for current page/total on `HandlePagingKey`/wheel — a small read seam to add.

### D5 — Registration in App.xaml.cs
Register `ISubMenuStrategy` (window strategy) + keep `RadialMenuSubMenuCoordinator` resolved via `MenuSession`'s existing `serviceProvider.GetService` style, or move to constructor injection. Prefer explicit DI registration over `GetService` to match the `StyleRendererFactory` precedent.

## Risks / Trade-offs

- [Breaking `IMenuSession` ripples to tests] → `WindowSwitchStrategyTests`/`GroupedSlotInteractionTests`/`MenuSessionTests` re-anchored on descriptor construction; window behavior asserted unchanged via the spec scenarios.
- [Deserialization of optional `subActions` mis-hits] → dedicated `ProfilesConfigDefaultsTests`/round-trip test for a slot with and without `subActions`; keep key absent (not null) for legacy files.
- [Session ↔ coordinator pagination seam drift] → expose explicit `GetSubMenuPageState()` from coordinator; session reads, never mutates.
- [Unknown strategy silent failure] → warning + root fallback is observable (logged) and spec'd; future Change B adds a strategy that makes this path reachable, not dead.
- [Over-generalizing before Change B lands] → no second strategy ships; `CascadeSubMenuDescriptor` exists but unused (guarded by spec, no UI), so no dead interactive path.

## Migration Plan

1. Introduce `SubMenuDescriptor`/`WindowSubMenuDescriptor`/`ISubMenuStrategy`/`SubMenuContext` and the strategy registry in `App.xaml.cs`.
2. Extract `WindowSwitchSubMenuStrategy` from the current `ConfigureSubMenu` body (behavior-preserving; run window-submenu tests green before any contract change).
3. Change `IMenuSession.EnterSubMenuAsync` signature + update `ProcessGroupStrategy`, `RadialMenuViewModel`, `MenuSession` to build descriptors. Re-run full suite; fix the compile break surface.
4. Add `SubSlots`/`SubActions`/`SubSlotDescriptor` + config tests (no migration needed).
5. Rollback: revert the interface change alone restores the old entry path (window behavior never regressed between steps 2 and 3).

## Open Questions

- None blocking. Whether `PluginSlot.SubActions` uses a nested DTO vs. flattened fields is a Change C decision and does not alter this change's specs.
