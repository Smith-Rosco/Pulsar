## Context

Grouped process slots in the radial menu currently support two distinct root-menu behaviors: modifier-release executes the highlighted slot, while mouse left-click drills into a submenu when the slot represents multiple windows. The execution path already uses the shared window-selection contract, but it currently identifies grouped-slot execution as a generic grouped switch that skips the pre-invocation window.

That generic grouped-switch behavior does not fully represent the user's confirmed intent for root-menu direct execution. The desired interaction is context-sensitive: when the user is outside the target app, the grouped slot should behave like a fast return to the app's most recently used window; when the user is already inside the target app, the same direct trigger should rotate to another recent window in that app. At the same time, submenu entry and submenu ordering must remain unchanged so explicit selection and muscle memory are preserved.

The change crosses the radial-menu interaction layer and the shared selection engine contract, so a design document is useful to lock down semantics before implementation.

## Goals / Non-Goals

**Goals:**
- Introduce an explicit selection intent for root radial-menu direct execution of grouped process slots.
- Make direct execution return to the target app's MRU window when the current foreground window is outside the target process.
- Make direct execution skip the current in-process window and target the next MRU eligible window when the current foreground window already belongs to the target process.
- Preserve left-click drill-down behavior and stable submenu ordering.
- Keep the change aligned with the existing shared selection contract rather than adding one-off ranking logic in the view model.

**Non-Goals:**
- Changing quick-switch behavior.
- Reordering submenu slots by MRU.
- Introducing per-app overrides or user-configurable selection policies.
- Changing WinSwitcher plugin semantics unless needed to keep the shared contract coherent.

## Decisions

### Decision: Model root grouped-slot direct execution as a dedicated selection intent
The design will add a dedicated window-selection intent for modifier-release execution from a grouped root slot, rather than reusing `GroupedSwitch` or `ProcessActivation`.

Rationale:
- The interaction has distinct semantics from both plugin-driven activation and submenu default selection.
- Encoding it as its own intent keeps the shared contract explicit and testable.
- It prevents future drift where root-menu execution semantics become coupled to unrelated grouped-switch entry points.

Alternatives considered:
- Reuse `GroupedSwitch` and special-case skip mode based on current foreground process. Rejected because it hides root-menu semantics behind conditional logic and weakens intent-driven tests.
- Reuse `ProcessActivation`. Rejected because grouped root-slot execution is not the same entry point as explicit plugin activation and has different fallback expectations.

### Decision: Base skip behavior on whether the current foreground window belongs to the target process
For the new direct-trigger intent, Pulsar will inspect the current foreground window and compare it against the target process group.

Behavior:
- If the current foreground handle belongs to one of the grouped candidate windows, selection skips that handle and chooses the next eligible candidate by recency.
- If the current foreground handle is outside the target process group, selection does not skip for in-app rotation and instead returns the top MRU candidate.
- If skipping would eliminate all candidates, selection falls back to the best ranked candidate rather than failing.

Rationale:
- This directly matches the user-approved scheme B.
- It avoids no-op feeling when the user is already in the same app.
- It preserves the existing fallback principle already used by the selection engine.

Alternatives considered:
- Always skip the pre-invocation window. Rejected because it produces the wrong result when the user invokes the menu from a different app and simply wants to return to the target app.
- Always skip the current foreground window. Rejected because when the current foreground window is outside the target process, that skip signal is irrelevant and obscures intent.

### Decision: Keep submenu order stable and independent from default target selection
The submenu will continue to display windows in stable order for muscle memory, while the center preview/default target logic remains free to use MRU-based selection.

Rationale:
- Existing specs and code already separate stable display order from recommended target selection.
- Reordering submenu items by MRU would create visual churn and broaden the scope of the change.

Alternatives considered:
- Reorder submenu slots by MRU. Rejected because it solves a different problem and would introduce larger UX change.

### Decision: Limit direct-trigger behavior change to modifier-release execution from the root menu
Mouse left-click on a grouped slot in the root menu will continue to enter the submenu instead of directly switching.

Rationale:
- The user explicitly confirmed that the new behavior should apply to modifier-release execution.
- Preserving click-to-drill-down keeps an explicit exploration path for users who want precise control.

Alternatives considered:
- Apply the same default-switch rule to left-click. Rejected because it would remove an established path to explicit window selection.

## Risks / Trade-offs

- [Risk] The current foreground handle may temporarily be Pulsar rather than the user's actual app window during execution. → Mitigation: resolve root-slot direct-trigger requests using the same foreground/pre-invocation awareness already present in the radial-menu flow and test both in-app and out-of-app cases.
- [Risk] Some apps expose auxiliary windows that rank highly by recency but are not the user's intended target. → Mitigation: keep the behavior bounded to the existing eligible-window inventory and preserve submenu drill-down for explicit correction.
- [Risk] Adding another selection intent increases contract surface area. → Mitigation: keep intent names narrow, add targeted tests, and avoid changing unrelated entry-point semantics.
- [Trade-off] The new behavior favors perceived usefulness over a single universal skip rule. → Mitigation: document the intent-specific rule clearly in specs and tests so the behavior remains explainable.

## Migration Plan

1. Extend the shared selection contract with a dedicated root grouped-slot direct-trigger intent and document its semantics.
2. Update grouped slot execution to use that intent only for root-menu modifier-release execution.
3. Keep left-click drill-down and submenu ordering unchanged.
4. Add or update tests covering out-of-app MRU return, in-app rotation to another recent window, and fallback when only one candidate exists.
5. Validate behavior manually in representative multi-window apps after implementation.

Rollback strategy:
- Revert grouped root-slot execution to the previous grouped-switch intent while leaving submenu behavior unchanged.

## Open Questions

- Should future work expose per-app policies for grouped direct-trigger behavior, or is one global rule sufficient?
- Should decision metadata explicitly indicate whether the current foreground window was considered in-process for the new intent, to make logs easier to interpret?
