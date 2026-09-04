## Context

See proposal.md — Why for motivation. Current state and constraints that shape this design:

- Pulsar is WPF + WinForms, .NET 8, MVVM + DI. The radial menu is a custom-drawn, borderless, always-on-top window opened by global hotkey/gesture. Its UIA tree is expected to be weak for custom-rendered content (no `AutomationPeer` on menu items today).
- Config is single-file `Profiles.json` under `%AppData%\Pulsar`, guarded by `ConfigEditSession` revision-based single-writer semantics. `ConfigService` already accepts a `configPath` override in its constructor (`Pulsar/Pulsar/Services/ConfigService.cs:106`), which debug-mode can reuse for isolation.
- `AppStartupCoordinator` (`Pulsar/Services/AppStartupCoordinator.cs`) is the single startup seam: hotkey init (`IHotkeyService.InitializeAsync`) and mouse/gesture init (`IGlobalMouseService.Initialize`) both run in `RunBlockingInitializationAsync` — the natural place to branch for debug mode.
- **No single-instance mutex exists in the codebase** (verified by search). The research report assumed one; this design does not depend on bypassing a mutex that does not exist. The isolated config dir still guarantees the debug instance never touches the user's real `Profiles.json`.
- Localization is enforced via `ILocalizationService`; user-facing strings are never hardcoded. UIA-based element lookup must therefore be AutomationId-driven, never text-driven.
- CI runner constraint: real SendInput requires an interactive desktop session (works on `windows-latest` GitHub Actions runners).

## Goals / Non-Goals

**Goals:**
- Design a closed loop: deterministic E2E (ground truth) + vision AI (fix proposal generator), with the AI never self-certifying.
- Make Pulsar externally drivable via a debug mode that cannot contaminate the user's real config or fire real global hotkeys.
- Define a diagnostic-package contract that is the single interface between the E2E framework and the AI loop.
- Design occlusion detection as a `visual-regression` check feeding both human acceptance and AI acceptance gates.

**Non-Goals:**
- No automation logic inside the application — the app only exposes a debug hook; all driving/asserting lives in the external `Pulsar.E2E` process.
- No AI CUA-style desktop agent that acts directly on the OS (rejected in proposal/specs for reliability).
- No changes to plugin contracts, `ConfigEditSession`, or the production startup path.

## Decisions

### D1. Application exposes only a passive debug hook; all intelligence lives in the driver
The app gains a `--ui-debug` flag that: redirects config to `%AppData%\Pulsar.Debug\Profiles.json`, skips global hook/hotkey/mouse-gesture registration, writes verbose logs to the debug dir, starts a named-pipe state publisher, and redacts PKI UI in captures. Everything else (workflow parsing, assertions, screenshots, recording, diagnostics) lives in the external `Pulsar.E2E` console project.
- **Why**: "应用不驱动自己" avoids input reaching its own message queue and masking timing bugs; the core scenario (global hotkey opening the menu) is inherently process-external.
- **Alternative**: in-app recorder/harness (rejected: pollutes app, masks real input timing).

### D2. Named-pipe state channel is the synchronization primitive
Debug instance publishes named events over a named pipe (e.g. `Pulsar.Debug.<pid>`), JSON payloads like `{"event":"menu-opened"}`, `{"event":"slot-activated","slotId":"..."}`. Driver subscribes with `waitForState` steps and a timeout; timeouts fail the step with a diagnostic.
- **Why**: UIA can't observe "menu actually opened" reliably for a custom-drawn window; blind sleeps are flaky. The pipe is the state oracle.
- **Alternative**: polling UIA tree for the radial window (`AutomationId:RadialWindow`) — kept as a secondary fallback assertion, not the primary sync.

### D3. Diagnostic package is the contract between E2E and AI
On failure the driver writes a deterministic directory (per run, per failing step):
`artifacts/<run-id>/<step-id>/` containing:
- `failure.json` — failed step, assertion message, timeout data
- `uia-tree.txt` — UIA automation tree dump (element id / name / bounds / enabled)
- `screenshot.png` — screen-level capture at failure moment (includes popups)
- `video.mp4` — recording clip (when recording active) via ScreenRecorderLib
- `logs/excerpt.log` — relevant log slice
This is the *only* input the AI loop consumes (per spec `visual-ai-iteration-loop`).
- **Why**: a stable, complete, self-contained failure artifact makes the AI iteration deterministic and lets human reviewers debug offline.

### D4. Element identity = AutomationId, never localized text
Core controls get stable `AutomationProperties.AutomationId` values; custom-drawn radial menu items get a custom `AutomationPeer` exposing `AutomationId`/`Name` and bounds. Workflow assertions use only AutomationId keys.
- **Why**: bilingual UI (EN/ZH) breaks text-based lookup; AutomationId is language-independent and survives layout changes.
- **Why custom peer not just attached property**: self-drawn content has no default peer; an attached property alone leaves the element invisible to UIA traversal.

### D5. Occlusion detection = screenshot + UIA bounds overlay + vision model
- Driver captures a stable full-screen screenshot (after animations settle) and projects the current `BoundingRectangle` of each interactive element as an overlay.
- The vision model consumes `(screenshot + overlay)` and outputs structured JSON occlusion report: `[{type: "interactive-overlap", overlaidId, occluderId, area, suggestion}]`.
- Expected overlays (menus, tooltips) go on an allow-list; only interactive-region overlaps are defects; purely decorative overlap is not reported.
- Report diffs against a per-view baseline; a clean report is the acceptance gate for that view's visual regression.
- **Why**: UIA bounds alone can't see rendered overlap (both elements still "exist" in the tree); pixel vision is the only reliable detector.
- **Why AI, not geometric intersection**: pure math over bounds produces false positives on transparent/rounded/cropped visuals; the vision model applies semantics.

### D6. External dependencies are bounded
`Pulsar.E2E` only: `FlaUI.UIA3` (UIA driver), `ScreenRecorderLib` (recording, phase later), `System.IO.Pipes` (BCL). The main `Pulsar` app takes **zero** new dependencies — debug mode is implemented with BCL-only (named pipe via `System.IO.Pipes`, config isolation via existing `ConfigService` ctor).
- **Why**: keeps the shipping app surface minimal and the E2E risk contained to the test project.

### D7. AI loop orchestration is a CLI, not a service
`Pulsar.E2E iterate --workflow <w.json> --max-iterations N` runs: execute workflow → on failure emit diagnostics → hand diagnostics to a configured LLM (image+text) → apply proposed patch → `dotnet build` → re-run the same workflow → repeat. Convergence is judged solely by the workflow result.
- **Why**: a single self-contained loop is reproducible in CI and locally, no long-running service to babysit.
- **Alternative**: long-lived agent service (rejected: operational overhead, harder to reason about).

## Risks / Trade-offs

- [SendInput requires foreground focus] → E2E is CI-safe gated (interactive-session pre-flight check per spec); element-based coords (UIA bounds) instead of hardcoded pixels to reduce DPI sensitivity; retry + recording for flake diagnosis.
- [Custom-drawn radial menu UIA tree is weak] → custom `AutomationPeer` for menu items + named-pipe state hook as the primary oracle; UIA is secondary.
- [Occlusion false positives (transparency, rounded corners, expected overlays)] → allow-list of overlay layers; only interactive-region overlap reported; baseline diffing.
- [Vision loop can diverge / hallucinate patches] → max-iteration cap; every iteration's diagnostics preserved for human review; the E2E gate is authoritative so a hallucinated patch simply re-fails.
- [Recording in CI requires VC++ redist + Win10 1903+] → phase gated; runner image validated in pre-flight.
- [Rogue local process could start a debug instance] → debug config dir is isolated from real data; PKI redaction is on by default in debug mode; acceptable for a local dev/test tool.

## Migration Plan

- Entirely additive: new `--ui-debug` path, new `Pulsar.E2E` project, new `AutomationId` attributes and `AutomationPeer` on core controls. Production behavior unchanged when the flag is absent. No data migration; no rollback path needed beyond removing the flag.
- Rollback for any stage: simply stop passing `--ui-debug`; the feature is inert by default.

## Open Questions

- Where the LLM provider sits in `Pulsar.E2E` (local Ollama vs hosted API) and its configuration surface — deferrable until the AI-loop phase, does not change specs or approach.
- Whether occlusion checks target only a curated set of "visual-critical" views (radial menu, settings shell) or all views — scope tuning at implementation time, gated by the same `visual-regression` acceptance requirement.
