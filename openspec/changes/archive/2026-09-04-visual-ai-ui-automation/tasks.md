## 1. Debug Mode (`--ui-debug`)

- [x] 1.1 Add `--ui-debug` command-line flag parsing in `App.xaml.cs` OnStartup and expose a debug-mode config object via DI; verify the flag is read and a `DebugModeOptions` is registered
- [x] 1.2 Redirect `ConfigService` to `%AppData%\Pulsar.Debug\Profiles.json` when debug mode is active (reuse existing `configPath` ctor arg) and verify a debug run writes only under the debug dir, leaving production `Profiles.json` untouched
- [x] 1.3 Branch `AppStartupCoordinator.RunBlockingInitializationAsync` in debug mode to skip `IHotkeyService.InitializeAsync` and `IGlobalMouseService.Initialize` hook registration; verify with a log assertion that hooks are not registered in debug mode
- [x] 1.4 Add PKI UI redaction in debug mode (mask PKI/secret-bearing views in capture output) and verify a debug capture shows masked PKI content

## 2. Named Pipe State Publisher

- [x] 2.1 Implement a debug-mode named-pipe server (`System.IO.Pipes`) that publishes JSON state events (`menu-opened`, `slot-activated`, `action-executed`) when the radial menu opens or a slot activates; verify events are emitted on a manual debug run
- [x] 2.2 Wire the publisher into the menu open / slot activation paths so the events actually fire; verify via a small test client reading the pipe during a real open
- [x] 2.3 Add unit tests covering the pipe event payload shape and event ordering

## 3. Core Control AutomationId + AutomationPeer

- [x] 3.1 Add `AutomationProperties.AutomationId` to core interactive controls (radial menu window, slot items, tray menu items, settings buttons); verify via a UIA dump that stable ids appear
- [x] 3.2 Implement a custom `AutomationPeer` for custom-drawn radial menu items exposing `AutomationId`/`Name`/`BoundingRectangle`; verify the radial menu slots are discoverable through a UIA client
- [x] 3.3 Ensure the radial window itself has a stable AutomationId for UIA-based fallback assertions; verify it appears in the UIA tree with the id

## 4. E2E Project and JSON Workflow Engine

- [x] 4.1 Create `Pulsar/Pulsar.E2E` console project (net8.0-windows) with `FlaUI.UIA3` referenced; verify it builds and runs an empty run
- [x] 4.2 Implement JSON workflow schema parsing supporting `launch`, `wait`, `waitForState`, `hotkey`, `click`, `assert`, `screenshot`, `record`, `exit` steps; verify a trivial workflow (launch+exit) executes end to end
- [x] 4.3 Add fixture configuration support so workflows launch a debug instance with a predetermined `Profiles.json` fixture; verify the launched instance loads the fixture, not the user config
- [x] 4.4 Implement the interactive-desktop-session pre-flight check that aborts with a clear diagnostic on non-interactive sessions; verify it fails cleanly on a non-interactive context
- [x] 4.5 Add unit tests for workflow parsing (unknown step type, missing step, malformed JSON produce clear errors)

## 5. FlaUI Driving and Assertions

- [x] 5.1 Implement AutomationId-based element lookup and `assert` step evaluation; verify an assertion on a known AutomationId passes and a missing one fails with a diagnostic naming the id
- [x] 5.2 Implement `hotkey` step using FlaUI `Keyboard`/`Mouse` (real SendInput) and verify it opens the radial menu in a debug instance
- [x] 5.3 Implement `waitForState` steps consuming the named-pipe events with a timeout; verify timeout produces a step failure diagnostic
- [x] 5.4 Implement `click` using UIA element `BoundingRectangle` center (not hardcoded pixels); verify a click on a fixture slot activates it

## 6. Screenshot, Recording, and Diagnostic Package

- [x] 6.1 Implement `screenshot` step via screen-level capture (FlaUI Capture / GDI CopyFromScreen) and verify a capture taken with a visible context menu includes the popup content
- [x] 6.2 Integrate `ScreenRecorderLib` for `record` steps and verify it produces an H.264 MP4 on a short recording run
- [x] 6.3 Implement the diagnostic package writer: on step failure emit `failure.json`, `uia-tree.txt`, `screenshot.png`, video clip (when recording), and `logs/excerpt.log` under `artifacts/<run-id>/<step-id>/`; verify a forced failure produces a complete package
- [x] 6.4 Implement the UIA tree dump for diagnostics; verify it contains element id / name / bounds / enabled state

## 7. Visual AI Iteration Loop CLI

- [x] 7.1 Implement `Pulsar.E2E iterate` command: execute workflow → on failure build diagnostic package → send to a configured LLM (image+text) → apply proposed patch → rebuild → re-run workflow; verify a deliberately broken XAML converges to green when the LLM returns a correct fix
- [x] 7.2 Implement max-iteration cap and final-diagnostic reporting; verify the loop stops at the cap and preserves the last diagnostic package for human review
- [x] 7.3 Add CLI option surface for workflow path, max iterations, and LLM provider config; verify options are validated

## 8. Occlusion Detection

- [x] 8.1 Implement stable-screenshot capture (post-animation settle) plus UIA bounding-box overlay projection; verify the overlay JSON matches element bounds on a target view
- [x] 8.2 Implement the vision-model occlusion analysis producing structured reports (`interactive-overlap` with overlaidId/occluderId/area/suggestion); verify two overlapping interactive elements produce a report entry
- [x] 8.3 Implement allow-list for expected overlays (menus, tooltips) and non-interactive decorative overlap filtering; verify an expected overlay and decorative overlap produce no defect
- [x] 8.4 Implement per-view baseline diffing and the acceptance gate (clean report required to pass visual regression); verify an unchanged view diffs identically against baseline and a changed layout either reports or is blocked
- [x] 8.5 Add sample fixture views/tests demonstrating the occlusion check running as part of the E2E suite

## 9. Integration and CI

- [x] 9.1 Add a canonical workflow `radial-menu-open-via-hotkey` (launch → record → hotkey → waitForState menu-open → assert slot AutomationId → screenshot → exit) and verify it passes on a debug instance
- [x] 9.2 Add GitHub Actions CI job running the core E2E workflows on `windows-latest`; verify the job passes in an interactive runner session and fails the pre-flight check cleanly if not
- [x] 9.3 Run `dotnet build` on the full solution and `dotnet test` on existing unit suites to confirm no regression from the new `AutomationId`/debug-mode changes
