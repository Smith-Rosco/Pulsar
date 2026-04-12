## Context

Pulsar already introduced a shared target-window selection helper and a shared activation path, which fixed the most visible divergence between WinSwitcher plugin switching and grouped radial switching. The remaining issues are structural rather than isolated bugs: `WindowService` still owns inventory, tracking, decision-making, activation, quick-switch state, focus restore, icon extraction, and blacklist management; `ProcessWindowInfo` still carries both synthetic and real recency values; submenu display ordering and default target choice still rely on different mental models; and blacklist semantics are described inconsistently between plugin metadata, docs, and runtime behavior.

The architecture must preserve Pulsar's muscle-memory-first philosophy from `ARCHITECTURE.md`. That means submenu display order may intentionally stay stable while actual switching decisions prefer recent usage, but the distinction needs to be explicit and testable rather than implicit in scattered call sites. The design also needs to avoid a large risky rewrite: the existing shared `SelectTargetWindow` and `ActivateWindow` paths should become migration anchors rather than be replaced wholesale in one step.

## Goals / Non-Goals

**Goals:**
- Define explicit service boundaries for window inventory, tracking, selection, quick-switch state, and activation.
- Replace ambiguous time semantics with explicit selection data: activation recency, Z-order rank, and stable display order.
- Move app-switching entry points onto one selection engine with explicit selection intent rather than a bare skip flag.
- Preserve existing end-user switching intents for WinSwitcher `activate`, `switch`, and `launch` actions.
- Make submenu behavior, quick-switch behavior, and blacklist behavior explicit enough to test and document.
- Enable incremental migration so current working flows can move to the new architecture in phases.

**Non-Goals:**
- Rework Pulsar's broader plugin architecture or hotkey lifecycle.
- Change the visible radial menu layout model outside the window-switching semantics covered here.
- Add new external dependencies or move switching logic out of process.
- Fully redesign preview capture, icon extraction, or unrelated `WindowService` helpers unless required by the boundary split.

## Decisions

### 1. Introduce a dedicated selection engine centered on explicit selection intent
Create a `WindowSelectionEngine` that accepts candidate windows plus a `WindowSelectionRequest` describing the switching intent. The request should express more than skip behavior, for example process activation, grouped slot switching, submenu default selection, or quick switch. This replaces the current thin `WindowSelectionContext` model with a request object that can still carry skip rules, but makes the reason for the choice explicit.

Rationale:
- Equivalent entry points should share ranking logic while still expressing intentional differences.
- The current `SkipMode` enum is enough for local fixes but not enough to explain or test product semantics.

Alternatives considered:
- Keep extending `WindowSelectionContext` with more flags. Rejected because it would continue to hide product semantics inside combinations of booleans and skip modes.
- Duplicate a separate selector per entry point. Rejected because that recreates the drift that the shared helper already started fixing.

### 2. Split inventory, tracking, selection, and activation into separate services
Introduce four explicit components:
- `WindowInventoryService`: enumerates eligible windows and produces raw metadata.
- `WindowTrackingService`: owns activation history, previous window, and any long-lived tracking registry.
- `WindowSelectionEngine`: turns candidates plus a selection request into a result with reason metadata.
- `WindowActivator`: performs the shared activation sequence for valid targets.

`WindowService` may remain as a facade during migration, but it should delegate to these components instead of retaining direct ownership of all behavior.

Rationale:
- These concerns change at different rates and need different test styles.
- Quick-switch state and selection policy are both stateful but they are not the same concern.

Alternatives considered:
- Keep one large `WindowService` with internal regions only. Rejected because it does not improve boundary clarity or testability.
- Split everything at once and rewrite all callers immediately. Rejected because it creates unnecessary migration risk.

### 3. Replace ambiguous recency fields with explicit ordering semantics
Selection inputs should distinguish at least:
- `ActivationTime`: real recency from the activation monitor / tracking registry.
- `ZOrderRank`: current visual stacking rank.
- `DisplayOrder` or `FirstSeenTime`: stable ordering for submenu presentation and muscle memory.

`ProcessWindowInfo.LastActivationTime` currently mixes synthetic Z-order recency into a name that implies user activation history. The new design should stop using that ambiguous field for selection decisions and migrate callers toward explicit semantics.

Rationale:
- User-facing switching should prefer real activation recency when it exists.
- Stable submenu positioning is valuable, but it is a different product concern than target-window ranking.

Alternatives considered:
- Keep current fields and rely on comments. Rejected because the naming itself misleads callers and caused the current confusion.

### 4. Model quick switch as a separate stateful component
Create a `QuickSwitchEngine` that owns history stack maintenance, active pair lifetime, and target resolution for quick-switch behavior. It should consume tracking data and activation services, but it should not live as incidental state inside the general-purpose switching facade.

Rationale:
- Quick switch behaves like a short-lived state machine with pair semantics and timeout rules.
- Keeping it embedded inside `WindowService` makes it harder to evolve without affecting unrelated switching paths.

Alternatives considered:
- Keep quick switch in `WindowService` and only add tests. Rejected because test coverage alone does not reduce conceptual coupling.

### 5. Preserve stable submenu display while making selection behavior explicit
The design keeps submenu display order stable by using explicit display-order data, while submenu default target selection uses the selection engine with a submenu-specific request. This aligns with Pulsar's muscle-memory-first philosophy while making the distinction deliberate.

Rationale:
- Stable positions aid spatial memory in a radial menu.
- Default target selection still benefits from recent-usage semantics.

Alternatives considered:
- Sort submenu items by MRU. Rejected as the default because it would weaken spatial consistency.
- Force submenu default target to be the first displayed item. Rejected because display order and switch intent serve different product goals.

### 6. Split blacklist semantics into discovery filtering and activation denial
The behavior contract should distinguish two concepts:
- Discovery blacklist: excludes processes from auto-discovered candidate lists.
- Activation denylist: explicitly blocks switch actions from targeting those processes.

This change should define which one WinSwitcher `ExcludeProcesses` maps to and update runtime behavior and docs accordingly. Based on current user-facing setting text, the default direction should be discovery filtering unless product decides otherwise.

Rationale:
- Current code and docs disagree, which makes behavior unpredictable.
- The distinction is necessary once selection is centralized.

Alternatives considered:
- Continue treating one list as both meanings. Rejected because it preserves the current conflict.

## Risks / Trade-offs

- [Migration complexity across multiple callers] → Keep `WindowService` as a temporary facade and migrate entry points in small steps, backed by selection tests.
- [Behavior regressions when replacing ambiguous time fields] → Add decision-focused tests that pin expected outcomes for tracked, untracked, current, previous, and submenu scenarios before wider refactoring.
- [Quick-switch refactor may break a working but fragile flow] → Move logic behind a dedicated component without changing pair semantics first; change behavior only after parity tests exist.
- [Stable submenu ordering may conflict with some users' MRU expectations] → Document the product rationale explicitly and keep the default target selection recent-aware even if display order remains stable.
- [Blacklist decision may need product confirmation] → Capture the ambiguity in specs and docs, and keep implementation behind the explicitly chosen behavior contract.

## Migration Plan

1. Define the new behavior-contract and architecture specs so code changes have explicit user-visible requirements.
2. Introduce explicit selection request/result types and migrate the shared selection helper to the new model while preserving current behavior.
3. Introduce `WindowInventoryService`, `WindowTrackingService`, and `WindowActivator` behind `WindowService` delegation.
4. Extract `QuickSwitchEngine` and migrate quick-switch flows with parity-focused tests.
5. Update submenu configuration and WinSwitcher flows to consume the new selection engine request types.
6. Remove or deprecate ambiguous fields and update docs to match final blacklist and submenu semantics.

Rollback strategy:
- Because the migration is facade-based, rollback can revert callers to the previous `WindowService` implementation boundaries if a phase introduces regressions before the old internals are removed.

## Open Questions

- Should `ExcludeProcesses` remain discovery-only, or should the product intentionally treat it as both discovery filtering and activation denial?
- Should focus-restore flows be fully routed through `WindowActivator`, or should some restoration paths remain separate if they need different OS-level handling?
- Does the team want submenu display order to remain `FirstSeenTime`-based, or should the stable order be formalized under a different explicit field name?
