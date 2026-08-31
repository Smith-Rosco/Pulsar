# ADR-010: Deepen the WindowService Module (Injectable Seams, Single Eligibility Evaluator, Capture Extraction, Inventory Coherence)

**Status**: Accepted (2026-08-31)
**Date**: 2026-08-31
**Deciders**: Pulsar Development Team

---

## Context

`WindowService` (1223 lines) is the composition root of the window-switching domain and has become a god module:

- **Five concerns in one class**: MRU/quick-switch, inventory enumeration + cache, eligibility, activation, and icon/thumbnail capture — plus the blacklist state, diagnostics flag, and the lifecycle of the activation monitor, event feed, and cleanup timer.
- **Five duplicated "build snapshot + evaluate" call sites** (`RecordWindowActivation`, `IsAltTabWindow`, selection diagnostics, pre-activation gate, post-activation re-check) re-derive the same composition (conditional title read, blacklist scope) independently. The pure, heavily-tested `WindowEligibilityPolicy` is right; its callers keep reinventing the protocol around it.
- **Zero tests construct `WindowService`**: all tests mock `IWindowService`, so the module's real orchestration (`SwitchToProcessAsync`, activation gates, MRU writes, cache invalidation) is untested. The extracted engines are only reachable through `internal static` factory seams.
- **The inventory cache coherence brain is scattered** across `OnWindowActivated` (invalidate-on-switch policy), `GetActiveWindowsAsync` (hit/miss fallback), `MenuSession` (seed-null-on-miss contract), and `ProcessPageProvider` (seeded vs live branch) — with two competing cache consumers and a hidden cross-file contract.
- **Icon extraction and thumbnail capture** (~160 lines of GDI/`DllImport`) with zero switching-domain coupling live inside the module; external consumers (`PreviewService`, submenu thumbnails) reach through the switching facade to fetch a bitmap.
- **Lifecycle gap**: `WindowEventFeed.Stop()`, `WindowActivationMonitor.Dispose()`, and the cleanup `Timer` are never stopped (dormant leak).
- **Dead code**: `_selectionEngine` field, `GetNextWindowInZOrder`, `RegisterOrUpdateWindow`, `ForceForegroundWindowAsync`.

## Decision

0. **Delete the dead members** first, so the carve starts from a clean base.

1. **Make the pure-logic collaborators constructor-injectable and registered in DI**: `IWindowEligibilityPolicy`, `IWindowInventoryService`, `WindowInventoryCache`, `QuickSwitchEngine`, `WindowTrackingService`. `WindowActivationMonitor`, `WindowEventFeed`, and the cleanup `Timer` stay `new`ed and owned by `WindowService`. `WindowService` implements `IDisposable` and stops them. **`IWindowInventoryService` is the only new collaborator interface** — justified because the inventory does native desktop enumeration and is `sealed` (cannot be Moq-faked); a test fake is the second adapter that makes the seam real. Other collaborators are injected as their concrete types (no speculative per-collaborator interfaces).

2. **Introduce a single eligibility evaluator**: `IWindowEligibilityEvaluator` owns snapshot assembly (incl. the conditional title read), an `EligibilityScope` enum (`Discovery` applies the process blacklist, `Explicit` ignores it), and the process blacklist; it composes `IWindowEligibilityPolicy`, which remains the pure rules engine (Exclude/Allow rules stay on the policy; the evaluator passes them through). The five call sites collapse to one `Evaluate(hwnd, scope)`.

3. **Extract capture and icon extraction** behind `IWindowCaptureService` (`CaptureWindowAsync`, `ExtractIcon`). Re-point `PreviewService` and `RadialMenuSubMenuCoordinator` at it; remove `CaptureWindowAsync` from the discovery facade. `WindowService` feeds `IWindowCaptureService.ExtractIcon` into enumeration as a delegate.

4. **Fold the inventory coherence brain into `IWindowInventoryCoordinator`**: owns the cache, the invalidate-on-real-switch policy, menu-dismiss prewarm (single-flight), and the hit→use / miss→enumerate fill policy. `WindowService` forwards activation events; `MenuSession` and `ProcessPageProvider` depend on the coordinator directly, so the seed-null-on-miss contract has one home.

**Kept**: the `IWindowService` facade aggregation (defer re-pointing its 24 callers — a separate widening change). New interfaces exist only where two adapters are real (production implementation + test fake): `IWindowInventoryService`, `IWindowEligibilityEvaluator`, `IWindowCaptureService`, `IWindowInventoryCoordinator`.

## Considered Options

- **New interface per collaborator** — rejected: every collaborator except the inventory has a single implementation; those would be hypothetical seams with no second adapter.
- **Move Exclude/Allow rules into the evaluator** — rejected: `WindowEligibilityPolicy` is already a deep, 523-line-tested pure engine; relocating its rules is zero-reward churn.
- **Keep the coherence logic in `WindowService`** (only deduplicate) — rejected: the deletion test fails; complexity would move between files in one class, not concentrate in a module.

## Consequences

- The window-switching orchestration — the source of almost every recent fix — finally has a regression net (`WindowService` is constructible with fakes).
- Eligibility scope semantics live in exactly one place; the five near-copies of snapshot/evaluate composition disappear.
- `MenuSession` and `ProcessPageProvider` talk to a coherence module, not the 1223-line facade.
- No change to the `IWindowService` facade surface: the 24 existing callers compile and behave identically.
- Deletes dead code and closes the lifecycle gap (`IDisposable`).
- Follow-up (explicitly out of scope): reconcile the twin "previous window" state machines (`WindowTrackingService` vs `FocusManager.IFocusHistory`).

---

**Change History**:
- v1.0.0 (2026-08-31): Initial version
