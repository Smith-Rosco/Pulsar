## Context

The bookmarklet runner currently uses a two-step execution path: it focuses the browser address bar, attempts UI Automation (UIA) text injection, and falls back to simulated typing if UIA fails. This fallback was intended to preserve execution success in browsers or page states where UIA cannot write to the focused address bar.

In practice, bookmarklet payloads are often long enough that simulated typing creates a worse failure mode than an immediate error. A transient readiness issue, such as a page still loading or the browser address bar not yet exposing a writable UIA surface, can leave the user unable to interact with the browser while a long `javascript:` payload is typed. That fallback can also leave partially entered address-bar content behind, which undermines the user's ability to retry cleanly once the page becomes ready.

This change is intentionally narrow. It does not redesign bookmarklet execution around a new transport such as clipboard paste. It only removes automatic typed fallback, standardizes fail-fast behavior, and makes retry safety an explicit contract.

## Goals / Non-Goals

**Goals:**
- Ensure bookmarklet execution uses UIA injection only.
- Ensure a failed UIA injection surfaces as a normal plugin failure that flows through Pulsar's existing user-facing notification path.
- Ensure failed attempts do not leave partial bookmarklet text or other plugin-induced residual input that would block or corrupt the next run.
- Preserve successful execution behavior when UIA injection works.

**Non-Goals:**
- Introducing a new clipboard-based bookmarklet injection mode.
- Improving browser-specific UIA compatibility beyond the current detection and focused-element write path.
- Redesigning the broader action-feedback architecture or plugin runtime.
- Guaranteeing cleanup of browser state that existed before Pulsar initiated the attempt.

## Decisions

### Decision: Remove automatic simulated-typing fallback entirely

The bookmarklet runner will treat UIA injection failure as the terminal outcome for that execution attempt.

Rationale:
- The current fallback optimizes for eventual execution, but bookmarklets have unusually large payloads and unusually poor UX when typed into a browser interactively.
- The most common expected failure mode described for this change is transient page readiness. In that case, an immediate retry after load completes is preferable to background typing.
- Pulsar already has a standard plugin error-to-notification path, so no bespoke fallback UX is required.

Alternatives considered:
- Keep fallback for short scripts only: rejected because it preserves two different failure models and keeps the plugin's retry semantics ambiguous.
- Add clipboard paste fallback: rejected for this change because it introduces a different side-effect profile and broadens scope beyond the immediate UX problem.
- Make fallback configurable: rejected for this change because the requested behavior is to remove fallback outright and establish one predictable execution contract.

### Decision: Define failure safety in terms of plugin-induced input state

The plugin will consider a failed run clean if it does not inject bookmarklet text or commit partial payload input after UIA failure.

Rationale:
- Pulsar can control whether it types the payload, but it cannot reliably restore arbitrary preexisting address-bar contents or browser-owned transient UI state.
- The current major source of contamination is plugin-driven text entry. Removing typed fallback removes the primary pollution vector.

Alternatives considered:
- Attempt to snapshot and restore prior address-bar content: rejected because it depends on browser-specific behavior, adds complexity, and risks creating new corruption modes.
- Send additional cleanup keystrokes after failure: rejected because extra synthetic input after a failed UIA path could itself become another pollution source.

### Decision: Reuse existing plugin error propagation and user-facing notification flow

The bookmarklet runner will return a recoverable `PluginResult.Error(...)` when UIA injection cannot complete. The plugin will not directly own tray-notification behavior.

Rationale:
- The existing `PluginActionStrategy` already converts plugin failures into normalized tray notifications.
- Centralized feedback keeps behavior aligned with other plugins and avoids duplicate notification logic inside the bookmarklet plugin.

Alternatives considered:
- Show notifications directly from the plugin: rejected because it duplicates an existing cross-plugin feedback path and mixes execution with presentation.

### Decision: Preserve minimal pre-injection setup only

The plugin may still hide the Pulsar window, focus the target browser, and send `Ctrl+L` before attempting UIA injection. If UIA fails, the plugin stops and reports failure without further text entry or execution keystrokes.

Rationale:
- These setup steps are required to reach the address bar for the supported success path.
- The harmful residual behavior starts when the plugin begins injecting payload text or pressing Enter after a failed injection attempt.

Alternatives considered:
- Remove `Ctrl+L` on failure-prone paths: rejected because success still depends on entering the address bar, and the requested concern is retry safety after a failed injection rather than preserving previous address-bar selection.

## Risks / Trade-offs

- [Reduced compatibility in some browsers or page states] → Users who previously relied on typed fallback will now see immediate failure. Mitigation: return a recoverable error with retry guidance and rely on the now-clean retry path.
- [Residual browser focus/address-bar selection remains after failure] → The plugin may leave the browser focused and the address bar selected because those are part of the setup path. Mitigation: define “no pollution” around avoiding payload injection or partial typed script, and document retry expectations around transient page readiness.
- [Generic notifications may be too vague] → Existing feedback mapping may collapse bookmarklet failures into a generic action-failed message. Mitigation: implementation should review whether bookmarklet-specific messaging is needed while still using the shared feedback pipeline.

## Migration Plan

1. Remove the simulated-typing branch from bookmarklet execution.
2. Ensure UIA failure returns a recoverable plugin error without sending payload text or Enter.
3. Add or update tests covering UIA failure, retry-safe behavior, and notification-path expectations.
4. Verify successful bookmarklet execution remains unchanged when UIA succeeds.

Rollback strategy:
- Reintroduce the typed fallback branch if the fail-fast behavior proves unacceptable in real usage. No data migration is required because the change affects runtime behavior only.

## Open Questions

- Should bookmarklet failures receive a dedicated user-facing feedback mapping such as “Page not ready” or “Browser input not ready,” or is the generic plugin failure notification sufficient for the first iteration?
- Do we want an explicit automated test at the plugin level only, or also a higher-level action-feedback test that confirms the user-visible notification intent for this plugin?
