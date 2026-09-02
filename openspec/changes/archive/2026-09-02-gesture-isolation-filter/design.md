## Context

See proposal.md — Why. The gesture path today swallows any right-button down that has a configured modifier held (`RadialMenuViewModel.FeedRightDragGesture`), with no foreground-window pre-filter. ADR-010's `IWindowEligibilityEvaluator` / `IWindowEligibilityPolicy` seam covers only the *window-switching* domain (Discovery/Explicit scopes, process blacklist for WinSwitcher). Native facts for a foreground-window snapshot already exist: `PulsarNative.GetForegroundWindow()` (`Pulsar/Pulsar/Native/PulsarNative.cs:145`) and `WindowService.GetForegroundWindow()` → `WindowInfo` (process name/path/title, `Models/WindowInfo.cs`). The existing `FeedRightDragGesture` reads `_configService` on config change and caches gesture settings as private fields — the isolation settings should follow the same refresh pattern.

## Goals / Non-Goals

**Goals:**
- Gate right-button takeover on a foreground-window isolation decision evaluated synchronously at button-down (hook thread), before any swallow or detector state change.
- Fullscreen detection with `Progman`/`WorkerW`/`Shell_TrayWnd` class-name bypass.
- Process allow-list/block-list dual mode, matched by foreground process name.
- Opt-in: disabled by default so existing behavior is unchanged; all settings persisted in `ProfileSettings`.
- Fully unit-testable (native facts behind an injectable seam; no OS coupling in the decision logic).

**Non-Goals:**
- Changing window-switching eligibility rules or `WindowEligibilityEvaluator` semantics — gesture isolation is a distinct behavior domain.
- Multi-trigger buttons (middle/side) — that is the separate `multi-trigger-buttons` change.
- Gesture isolation on release or during move — the decision is made once at button-down only.

## Decisions

### D1: Dedicated `IGestureIsolationService`, not a decorator over `IWindowEligibilityEvaluator`

The roadmap suggested reusing the ADR-010 evaluator seam as a decorator, but gesture isolation and window-switching eligibility are different concerns with incompatible semantics: the switching evaluator applies the user Exclude/Allow *rules* and structural verdicts (cloaked/owned/toolwindow) that must NOT gate a gesture, and its `EligibilityScope` has no gesture notion. Forcing it through the switching evaluator would couple input interception to the switch-rules engine and leak Exclude/Allow rules into gestures.

- **Chosen**: new `IGestureIsolationService` interface (production impl `GestureIsolationService`) injected into `RadialMenuViewModel`. New interface is justified per ADR-010's own rule ("interface only where two adapters are real" — production impl + Moq test fake).
- **Alternative rejected**: decorator over `IWindowEligibilityEvaluator` — reuses the seam but drags switching rules into gesture input; `UpdateBlacklist`/`UpdateRules` are switch-scoped and would need awkward repurposing.

### D2: Synchronous native snapshot on the hook thread

`FeedRightDragGesture` runs on the hook thread for the modifier check today. `GetForegroundWindow` + `GetClassName` + `GetWindowProcessId`/`GetProcessName` + `GetWindowRect` are cheap P/Invokes; right-button downs are rare events. Evaluating synchronously keeps the swallow/no-swallow decision atomic with the event, matching StarPie's `CheckIsIsolated` placement.

- Native reads isolated behind an injectable `IGestureIsolationNative` adapter (like `IFocusNativeAdapter` precedent in `Services/Interfaces/IFocusNativeAdapter.cs`), returning a plain `ForegroundWindowFacts` record (ClassName, ProcessName, WindowRect, MonitorBounds). The decision logic (`GestureIsolationService`) operates only on that record + config → pure, Moq-testable.
- **Alternative rejected**: dispatch to UI thread first — adds a hop and a race window where the down could pass through before the decision lands; the whole point is atomic claim.

### D3: Fullscreen = foreground rect covers the monitor under the cursor

Compare the foreground window rect against the monitor bounds (from the cursor position), with a small tolerance, and NOT against the work area. Shell surfaces (`Progman`/`WorkerW`/`Shell_TrayWnd`) are short-circuited out of the fullscreen branch by class name so a desktop/taskbar right-click is never misclassified as fullscreen.

- Trade-off: a *maximized* window whose rect covers the full monitor bounds can be classified as fullscreen (borderless-fullscreen and maximized are visually similar to `GetWindowRect`). Mitigation: this only matters when the user enables isolation; the block-list/allow-list still applies, and the fullscreen toggle is configurable. StarPie ships the same coarse check; refine later if flagged.

### D4: Isolation settings in `ProfileSettings`, refreshed like other gesture config

Add to `ProfileSettings` (`Models/ProfilesConfig.cs`):
- `GestureIsolationEnabled` (bool, default `false`) — master switch; when `false`, all gesture presses are eligible (spec: "Filter disabled preserves current behavior").
- `GestureIsolationMode` (enum `Allowlist`/`Blocklist`, default `Allowlist`).
- `GestureIsolationProcesses` (list of process names, case-insensitive, default empty).
- `GestureIsolationBlockFullscreen` (bool, default `true`) — when enabled, fullscreen foreground denies the gesture.

`RadialMenuViewModel` caches these in `RefreshGestureConfig()` (same path as existing gesture fields) and applies them in `ApplyPendingGestureConfig()` — no per-event config read. Evaluation order at right-down: (1) configured gesture modifier held? (2) isolation filter enabled? if yes evaluate → denied ⇒ pass through without touching the detector; (3) proceed with the existing `OnRightDown`/pending paths.

### D5: Denied gesture never enters the state machine

The isolation check runs *before* `_gestureDetector.OnRightDown` and before the no-modifier "pending swallow" leak-fix branch. A denied right-down returns `false` immediately (passes to the app), leaves `IsPressed`/`IsSummoned` untouched, and is not recorded as `_pendingGestureDown`. This satisfies the spec scenarios "Gesture denied passes the click through" and "Gesture denied by isolation never enters the state machine".

## Risks / Trade-offs

- **Maximized-window false positive in fullscreen detection** → coarse rect-vs-bounds check, short-circuit on shell classes, configurable `GestureIsolationBlockFullscreen`; revisit with a style-bit check (e.g. no `WS_CAPTION`/`WS_THICKFRAME`) only if users report it.
- **Per-right-down native snapshot cost** → negligible (a few P/Invokes on a rare event); if it ever matters, cache `GetMonitorInfo` per cursor monitor.
- **Case-insensitive process matching** — order-insensitive, trimmed entries; a malformed entry is ignored, never throws.
- **Config race during gesture** → same deferred-apply mechanism as existing gesture fields (never mutate a pressed detector's gating config mid-press); denied verdicts only matter at the instant of a new down.

## Migration Plan

- No breaking change: new settings default off; existing users keep current behavior. Settings UI adds the isolation section behind the existing gesture settings area; plain JSON fields on `ProfileSettings` deserialize to defaults when absent.

## Open Questions

- Whether the fullscreen toggle should also block when the filter's master switch is off — no: `GestureIsolationEnabled=false` means the entire filter (including fullscreen) is inert, preserving current behavior exactly.
- Whether `WorkerW` should also be excluded from the process lists entirely — currently only excluded from the *fullscreen* branch; an allow-list/block-list entry for a shell process still works. No spec change required either way.
