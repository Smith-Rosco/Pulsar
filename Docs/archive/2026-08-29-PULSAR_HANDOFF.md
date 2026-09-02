# Handoff — Pulsar radial-menu "empty wheel then fill" fix

Date: 2026-08-29
Repo: `E:\8_Project\10_C#\Pulsar_Project` (branch `main`, Windows / pwsh)
Note: generic agent rules & project conventions live in `AGENTS.md` at repo root.

## 1. Goal

When the radial menu is summoned it briefly shows an **empty wheel**, then fills
with content ~a moment later. Historically it appeared fully populated. The
desired end state: the menu shows real content on the **first frame** while a
cold (uncached) window enumeration must still never delay the menu's appearance.

## 2. Root cause (already diagnosed — do not re-investigate)

Traced via `git log`/`git show`; no need to repeat the archaeology.

- Commit `3fe5de5` ("perf(menu): two-phase show with background content load and
  window inventory cache") split `BeginSessionAsync` into Phase 1 (surface shell
  immediately, `IsVisible = true` before content load) + Phase 2 (background
  `LoadPageContentAsync`). The empty-wheel flash is the direct consequence: the
  shell now shows before `RefreshVisuals` runs.
- The **motivating delay** predates 3fe5de5: the pre-3fe5de5 show path awaited
  `GetActiveWindowsAsync()` (~200ms full desktop enumeration, growing heavier
  through `00cdfca` / `5c8c900`) before `IsVisible = true`, so summoning had a
  visible lag. Two-phase moved that lag from "before show" to "inside show".
- The enumeration itself was expensive for avoidable reasons: per window it ran
  `Process.GetProcessById` + `MainModule` (~full system snapshot + module walk)
  BEFORE the eligibility filter rejected the window — ~300 expensive calls to
  produce ~20 eligible results.

## 3. Work done — UNCOMMITTED working-tree changes (all three parts)

Baseline was clean before this work. All changes are **not yet committed** —
`git status` / `git diff` to inspect. Three parts, each verified by the full
test suite (630/630 passing):

### Part 1 — warm-cache synchronous fill + dismiss pre-warm
Design: keep two-phase as the cold-cache fallback; when the Switch-mode inventory
cache is already warm, load+apply content synchronously.

- `IWindowDiscoveryService.cs` / `WindowService.cs`:
  `TryGetCachedActiveWindows(out ...)` (probe without enumerating) +
  `PreWarmWindowInventory()` (single-flight background refresh, new `force` param).
- `ProcessPageProvider.cs`: optional ctor param `seededWindows`; `LoadAsync`
  builds slots from it instead of re-enumerating.
- `MenuSession.cs`: pre-surface warm apply + dismiss-time pre-warm (Task mode only).
- Tests: 3 added in `MenuSessionTwoPhaseOpenTests.cs`.

### Part 2 — Option A: structural-first enumeration filter (the cause fix)
Reordered the filter so expensive process-metadata resolution only runs for
surviving windows, and swapped `MainModule` for `QueryFullProcessImageName`.
Estimate: 200ms → ~20-40ms full enumeration.

- `WindowEligibilityPolicy.cs`: split `Evaluate` into `EvaluateStructural`
  (no ProcessName needed) + `EvaluateIdentity` (blacklist + user rules). `Evaluate`
  = structural then identity, so single-shot callers keep identical semantics.
- `WindowEligibilitySnapshot.cs`: `FromHwndStructural` is now the single native
  snapshot builder (both `FromHwnd` and the enumeration path go through it — this
  also fixed a latent drift where the enumeration path never set `Style`, so the
  WS_CHILD rule never fired there). `Style` is now read on both paths.
- `WindowInventoryService.cs`: enumeration runs `EvaluateStructural` first (~300
  cheap native calls), then resolves process metadata via `ProcessMetaResolver`
  only for survivors (~20), memoized by pid within the pass.
- `ProcessMetaResolver.cs` (new): `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`
  + `QueryFullProcessImageName` — one targeted call, no module walk, no exception
  on cross-bitness/protected processes. Fallback to `Process.GetProcessById` for
  the name only when the handle can't be opened.
- `PulsarNative.cs`: added `OpenProcess`, `QueryFullProcessImageName`,
  `CloseHandle` + `PROCESS_QUERY_LIMITED_INFORMATION`.
- `WindowEligibilityPolicyTests.cs`: 4 new tests (structural/identity split,
  equivalence of one-shot vs two-stage verdicts).
- `ProcessMetaResolverTests.cs` (new): 6 tests incl. memoization + no `.exe` suffix.

### Part 3 — Option C: deadline-bounded single-phase load (the flash fix)
`MenuSession.BeginSessionAsync` now races the content load against a **first-frame
budget** (default 50ms, injectable via ctor `TimeSpan? firstFrameBudget`):

- One code path for warm and cold caches: start `LoadPageContentAsync` (seeded
  from the cache when available), race it against `Task.Delay(budget)`.
  - Load wins the budget → model applied **before** `IsVisible = true` → fully
    populated first frame (single-phase in practice, since Part 2 made the
    enumeration ~20-40ms).
  - Budget expires first → shell surfaces within the budget, the in-flight load
    patches content into the visible shell (bounded two-phase fallback).
- `LoadPageContentAsync` is now `Task<bool>` with a **single** apply path (the old
  `applyBeforeSurface` divergence and `!IsVisible` guard are gone — dismissal
  cancels the session token, newer sessions bump the generation, so
  token+generation guard alone is correct for both pre- and post-surface applies).
- Phase-2 retry is guarded: only a genuine failure (exception) on the still-current,
  still-visible session retries — dismissal/supersession never triggers a retry.
- `MenuSessionTwoPhaseOpenTests.cs`: existing tests updated to the new timing
  contract (shell surfaces within the injected 5ms budget, not synchronously);
  2 new tests (fast-cold-load single-phase, slow-load patch-in-after-surface).

## 4. What was decided and why (supersedes the earlier "timer/TTL" plan)

The previous handoff's approved plan (10s background timer + 30s TTL + startup
pre-warm) was **replaced** by Part 2 + Part 3 above, per the user's "follow your
advice" decision. Rationale (from the session where Options A–D were analyzed):

- Option A removes the *reason* two-phase exists (enumeration now ~20-40ms), so
  the load lands inside a 50ms budget essentially always → single-phase in practice.
- Option C collapses the warm/cold branching into one path with a hard guarantee
  that summoning never blocks beyond the budget — smaller diff than the timer/TTL
  plan and fixes the cause rather than papering over it with a background poll.

The timer/TTL plan is **not** implemented. Do not add it unless a regression
appears. If one does, note the tradeoffs flagged in that analysis: a 10s timer is
a real idle-machine tax, and TTL-vs-timer alignment silently disables the warm
path (the footgun the deadline race removes entirely).

## 5. Verification status

- `dotnet build` clean: `Pulsar`, `Pulsar.Simulator` (0 warnings, 0 errors).
- `dotnet test` **630/630 passing** (was 617 pre-change), incl. the 3 new Part-1
  tests, 4 new Part-2 policy tests, 6 new Part-2 resolver tests, and 2 new Part-3
  tests. TwoPhaseOpenTests updated to the deadline-bounded contract.
- Real latency measurement still pending: the constant factors (the ~200ms →
  ~20-40ms estimate, the 50ms budget choice) were read off code paths. Get a real
  number from the `[MenuTiming]` log lines (`Show.Surface` / `Show.Load` /
  `Show.Apply`) before tuning the budget. `SlowLoadThresholdMs` = 40 logs "cache
  miss" loads at Information level.

## 6. Known pitfalls / invariants (from this repo's lessons)

- Never query live window state in plugins — always `PulsarContext`.
- The budget race is safe under dismissal/supersession only because dismiss
  cancels the session token (`_sessionCts`) and a newer session bumps
  `_sessionGeneration` — the apply and the retry both guard on token+generation.
- `ProcessMetaResolver` memoizes only within a single enumeration pass (pid reuse
  is safe in that window; never cache across enumerations without process-creation
  time checks).
- `FromHwndStructural` is the single snapshot builder — never hand-assemble a
  `WindowEligibilitySnapshot` in a new path or a field can silently drift again
  (the historical `Style`/WS_CHILD bug).
- `LoadPageContentAsync` returning false retries only on genuine failure while the
  session is current + visible; a dismissed/superseded load must stay discarded.
- Lessons index: `Docs/lessons/`. Build commands: `Docs/ops/BUILD_AND_RUN.md`.

## 7. Suggested skills (call the Skill tool for these)

- `diagnosing-bugs` — if a follow-up reports the empty-wheel flash persisting or
  reappearing, use this loop (logs at `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`,
  `[MenuTiming]` lines instrument Surface/Load/Apply segments).
- `handoff` — regenerate a fresh handoff when the next session finishes.

## 8. Sensitive info

None in this doc. Do not copy secrets/keys; repo rule: never commit secrets.
