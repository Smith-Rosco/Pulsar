## Context

Pulsar currently supports application switching through several entry points that should feel equivalent to users but do not currently share the same target-selection rules. `WinSwitcherPlugin` delegates process switching to `WindowService.SwitchToProcessAsync`, grouped radial menu slots delegate to `WindowService.SelectTargetWindow`, and direct window slots bypass `WindowService` activation behavior by calling native APIs from UI strategies.

The current design also mixes multiple notions of recency in shared models. `WindowService.GetActiveWindowsAsync()` populates `LastActivationTime` from synthetic Z-order ordering while also carrying `RealActivationTime` from the activation monitor. Different callers then sort or skip windows using different fields and different exclusion rules. This makes the switching stack hard to reason about and causes behavior drift between plugin execution and radial menu execution.

The proposed change intentionally focuses on the highest-return core refactor first. It does not attempt to fully decompose `WindowService` or redesign Quick Switch. Instead, it establishes a shared decision path for selecting and activating windows, then adapts the existing entry points to consume that path.

## Goals / Non-Goals

**Goals:**
- Define a single selection model for choosing a target window from a set of candidates.
- Make WinSwitcher plugin switching and grouped radial-menu switching use the same selection rules for multi-window processes.
- Standardize which recency signal is used for user-facing selection decisions.
- Centralize window activation behavior so restore-and-foreground logic is not split across service and UI layers.
- Preserve the documented `activate`, `launch`, and `switch` semantics exposed by the WinSwitcher plugin.
- Keep the first implementation phase small enough to validate with focused tests and low regression risk.

**Non-Goals:**
- Fully extract Quick Switch into a dedicated service or state machine.
- Split all `WindowService` responsibilities into separate inventory, tracking, capture, and activation services.
- Redesign submenu presentation, icon loading, or process grouping UX beyond the minimum changes required for consistent switching.
- Change user-authored plugin slot formats or introduce new user-facing WinSwitcher actions.

## Decisions

### Decision: Introduce a shared selection core inside the existing window switching layer first

The first implementation phase will introduce a dedicated selection abstraction that accepts candidate windows and a selection context, then returns a target window using a shared rule set. This selection core may live inside `WindowService` initially or as a narrowly scoped helper/service adjacent to it, but all non-Quick-Switch app-selection entry points will delegate to it.

Rationale:
- This yields the highest behavioral consistency for the least code movement.
- It lets us unify WinSwitcher plugin and grouped-slot behavior without a large service extraction.
- It creates a seam for future extraction if the selection logic stabilizes.

Alternatives considered:
- Keep separate selection methods and align them informally. Rejected because behavior drift is already a problem.
- Fully split `WindowService` before any behavioral change. Rejected because it expands risk and scope before improving user-facing consistency.

### Decision: Use real activation history as the canonical recency signal for selection decisions

Selection decisions for user-facing switching SHALL use activation-monitor-backed recency rather than synthetic Z-order timestamps. If a candidate window lacks tracked activation history, the selection core will treat it as lower-confidence fallback data rather than equal to a real activation record.

Rationale:
- The product intent is app switching based on recent interaction, not visual stacking heuristics.
- The codebase already has activation tracking infrastructure; the problem is inconsistent consumption, not lack of signal.

Alternatives considered:
- Continue using Z-order-derived timestamps for grouped slot selection. Rejected because it preserves divergent behavior.
- Remove all fallback behavior for untracked windows. Rejected because newly opened windows still need a deterministic path.

### Decision: Make skip rules explicit through selection context rather than hardcoded per caller

The selection core will accept contextual flags or mode values to decide whether to skip the current foreground window, the Pulsar pre-invocation window, or neither. This keeps behavioral differences explicit while still sharing one algorithm.

Initial modes are expected to cover:
- process activation from WinSwitcher plugin actions
- grouped radial-slot switching while Pulsar is foregrounded
- direct single-window activation when a concrete window is already chosen

Rationale:
- Current divergence is caused as much by different skip rules as by different recency data.
- Explicit modes make future behavior review and test coverage much clearer.

Alternatives considered:
- Force identical skip behavior in every switching path. Rejected because plugin-driven switching and menu-driven switching do not always have the same foreground context.

### Decision: Centralize restore-and-foreground behavior behind a single activation path

Foreground activation of an already selected window will be routed through a single service-level method so callers do not decide independently whether to restore minimized windows, call `SetForegroundWindow`, or handle invalid handles.

Rationale:
- The current split between `WindowService` and `WindowSwitchStrategy` duplicates behavior and increases regression risk.
- A single activation path improves logging, testability, and future compatibility fixes.

Alternatives considered:
- Leave direct window activation in UI strategies. Rejected because it prevents consistent behavior and instrumentation.

### Decision: Defer full `WindowService` decomposition and Quick Switch extraction

This change will not reorganize every responsibility in `WindowService`. It will only carve out or introduce the minimal seams needed to unify selection and activation for the highest-value paths.

Rationale:
- The user explicitly asked to prioritize by benefit and avoid covering every optimization at once.
- Full decomposition is valuable, but it should follow a validated shared selection core rather than precede it.

## Risks / Trade-offs

- [Regression in Quick Switch expectations] -> Keep Quick Switch behavior out of scope for the first refactor except where shared activation helpers are reused.
- [Untracked windows may rank unexpectedly] -> Define deterministic fallback ordering for windows missing activation history and cover it with tests.
- [Behavior changes may surface hidden dependency on synthetic Z-order ordering] -> Add regression tests for grouped slot switching and WinSwitcher plugin switching before broadening scope.
- [Service boundary remains imperfect after phase one] -> Accept a temporary intermediate design as long as the shared selection path is real and future extraction becomes easier.
- [Existing UI code may rely on `ProcessWindowInfo` field semantics] -> Clarify and document which field is canonical for switching decisions before updating callers.

## Migration Plan

1. Introduce the shared selection abstraction and adapt the WinSwitcher plugin path to consume it.
2. Adapt grouped process-slot switching to consume the same selection abstraction.
3. Introduce or expose a single activation path and route direct window activation through it.
4. Add targeted regression tests for multi-window process selection, skip-rule behavior, and minimized-window activation.
5. Validate behavior manually with common multi-window apps such as Explorer, VS Code, Chrome, and Excel.

Rollback strategy:
- Revert the caller wiring to the previous methods while retaining any non-invasive test additions.
- Because this change does not alter stored configuration formats, rollback is code-only.

## Open Questions

- Should submenu presentation continue to prefer stable ordering, even if selection decisions use real activation recency?
- Should a user-configured blacklist exclude an app only from automatic discovery or also from explicit switch actions?
- Is it sufficient to keep the shared selection abstraction inside `WindowService` for now, or is there already enough complexity to justify a new dedicated service in phase one?
