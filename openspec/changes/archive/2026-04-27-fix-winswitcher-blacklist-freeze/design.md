## Context

The WinSwitcher blacklist configuration flow currently couples a foreground settings dialog to heavyweight runtime services. Opening the dialog triggers full active-window discovery, icon extraction, and background process-registration work even though the UI only needs a process list, current blacklist state, and a lightweight indication of whether a process is running.

The current implementation also applies the same blacklist predicate to both discovery-time candidate enumeration and explicit process-targeted activation. That conflicts with the documented `ExcludeProcesses` semantics, which describe a discovery blacklist rather than an activation denylist.

This change crosses dialog view models, window inventory services, and process registry behavior, so it benefits from an explicit design before implementation.

## Goals / Non-Goals

**Goals:**
- Keep the WinSwitcher blacklist dialog responsive even when the process registry is large or icon loading is slow.
- Separate configuration-facing data needs from full runtime window inventory behavior.
- Remove hidden write-side effects from blacklist-related read flows used by foreground UI.
- Re-align runtime behavior with documented discovery-only blacklist semantics.
- Make the new behavior testable at service and view-model boundaries.

**Non-Goals:**
- Redesign the broader WinSwitcher settings UX beyond responsiveness and correctness.
- Introduce a new activation denylist feature in this change.
- Rework unrelated window selection, activation, or quick-switch ranking logic.
- Replace the existing process registry or icon cache architecture wholesale.

## Decisions

### 1. Introduce a lightweight blacklist-dialog data path
The blacklist dialog should stop using the same full-fidelity inventory path used by runtime switching features. Instead, it should consume a lightweight process-oriented data path that provides:

- known processes from the registry
- current blacklist state
- lightweight running-state membership
- deferred or placeholder icon behavior

Rationale:
- The dialog does not need window handles, titles, selection metadata, or process-registration side effects.
- A lighter path reduces UI stalls and limits coupling between configuration surfaces and runtime switching services.

Alternatives considered:
- Keep using `GetActiveWindowsAsync()` and optimize it further. Rejected because the API itself returns more data and more side effects than this UI needs.
- Fully remove running-state indicators. Rejected because the indicator is useful and can be preserved through a cheaper lookup path.

### 2. Move expensive icon work off the critical first-render path
The dialog should render usable rows before all icons are available. Icon loading should be deferred, batched, or lazily applied so the initial dialog open is bounded by lightweight metadata rather than per-row image extraction and decoding.

Rationale:
- The current flow performs per-process icon lookup before the dialog list is complete.
- Slow or problematic icon sources should degrade the icon experience, not block the entire dialog.

Alternatives considered:
- Load every icon synchronously before binding rows. Rejected because this is the main source of UI stalls.
- Drop icons entirely. Rejected because it reduces usability and is unnecessary if icon loading becomes non-blocking.

### 3. Remove inventory query side effects from foreground configuration loads
Foreground configuration reads should not trigger process registration, cache persistence, or unrelated background mutation. If process registration is still needed elsewhere, it should be initiated explicitly by runtime flows that actually require it.

Rationale:
- Query-time side effects make configuration opens expensive and hard to reason about.
- Side-effect-free reads are easier to test and less likely to contend with file locks or cache writes.

Alternatives considered:
- Keep side effects but schedule them differently. Rejected because configuration reads still should not implicitly mutate runtime state.

### 4. Preserve discovery-only blacklist semantics for explicit activation
Discovery blacklist checks should remain on auto-discovery paths only. Explicit process-targeted activation should not reuse the discovery blacklist predicate unless a separate activation-denial concept is introduced later.

Rationale:
- This matches current product documentation and existing user expectation.
- It keeps behavior consistent across documentation, settings UI language, and runtime code.

Alternatives considered:
- Change the docs to match current code. Rejected because discovery-only semantics are already the clearer, safer contract and align with the blacklist UI wording.

## Risks / Trade-offs

- [Risk] Deferred icon loading could cause visible placeholder-to-icon transitions. → Mitigation: use stable fallback icons and update rows incrementally without blocking first render.
- [Risk] Splitting lightweight running-state lookup from full inventory could duplicate process-query logic. → Mitigation: keep the lightweight API narrow and shared through a dedicated service method instead of duplicating ad hoc code in the view model.
- [Risk] Removing registration side effects from inventory reads could reduce passive registry refresh behavior some flows were relying on implicitly. → Mitigation: identify and update the actual runtime flows that need registration so they call it intentionally.
- [Risk] Adjusting blacklist semantics could change behavior for users who accidentally depended on the current bug. → Mitigation: preserve the documented contract and note the correction in release notes or plugin docs if needed.

## Migration Plan

1. Introduce the lightweight data path and update the blacklist dialog to use it.
2. Move icon loading out of the initial blocking list-construction path.
3. Remove or relocate process-registration side effects from configuration-facing inventory reads.
4. Split discovery blacklist checks from explicit activation paths and add regression tests.
5. Validate the dialog against large registries and explicit activation scenarios before release.

Rollback strategy:
- Revert the blacklist dialog to the previous data path if the lightweight path introduces correctness regressions.
- Re-enable explicit registration calls in runtime flows individually without restoring hidden read-side effects globally.

## Open Questions

- Whether the lightweight running-state lookup should be window-based, process-based, or a hybrid for the best balance of correctness and cost.
- Whether the dialog should load icons only for visible rows or via bounded background batches.
- Whether a future explicit activation denylist capability is needed, or whether discovery-only semantics are sufficient long term.
