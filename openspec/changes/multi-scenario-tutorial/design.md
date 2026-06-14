## Context

The current tutorial is a single 6-step flow (Notepad → sendkeys) hardcoded in `Assets/TutorialSteps.json`. The setup wizard (`FirstLaunchSetupWizardViewModel`) offers 3 usage profiles via `OnboardingUsageProfile` enum, but each profile only swaps the single command slot — the tutorial steps remain unchanged. There is no prerequisite detection: a user could select "Browser & Docs" without a browser installed and get a non-functional tutorial.

The proposal adds multi-scenario support (Notepad, Excel VBA, Browser Bookmarklet), prerequisite validation, step transition improvements, and slot-missing fallbacks. This design covers the architecture for scenario abstraction, prerequisite system, step loader routing, and UI integration.

## Goals / Non-Goals

**Goals:**
- Introduce `TutorialScenario` as a first-class model replacing the flat enum
- Support per-scenario slot templates (one tutorial-primary slot + optional extras)
- Support per-scenario step definitions (JSON files sharing the 6-step structure)
- Detect prerequisite software per scenario and surface status in the setup wizard
- Fall back when prerequisites are partially met (e.g., Excel exists but VBA missing → use sendkeys)
- Improve step 2→3 transition (auto-skip success confirmation when switch action completes)
- Provide step 2 slot-missing fallback (guide user to Settings when no slot found)

**Non-Goals:**
- No runtime prerequisite re-checking during tutorial execution (install-time only)
- No automatic software installation
- No dynamic step generation — steps remain static JSON per scenario
- No Mac/Linux prerequisite detection
- No changes to plugin execution logic (VbaRunner, BookmarkletRunner, CommandPlugin unchanged)

## Decisions

### D1: TutorialScenario as a class, not an enum

**Decision**: Replace `OnboardingUsageProfile` enum with a `TutorialScenario` class registered in a `TutorialScenarioRegistry` singleton.

**Rationale**: An enum cannot carry metadata (slot templates, prerequisite provider ref, steps JSON path). A class lets each scenario declare its full configuration in one place. The enum stays for backward compat but is wrapped.

**Alternatives considered**: Config-driven scenarios (JSON-only, no C# class). Rejected because prerequisite checkers need code (registry queries, file I/O) that can't live in JSON.

### D2: Per-scenario JSON step files, not single file with scenarios section

**Decision**: One JSON file per scenario: `TutorialSteps.json` (default/Notepad), `TutorialSteps.excel.json`, `TutorialSteps.browser.json`.

**Rationale**: Single-file-with-sections works but grows to 400+ lines and makes diffs harder to read. Per-file keeps each step set self-contained and easy to version.

**Alternatives considered**: Single JSON with `"scenarios": { "notepad": {...}, "excel": {...} }` — rejected for maintainability.

### D3: Prerequisite checkers are code classes referenced by type name

**Decision**: `TutorialScenario.PrerequisiteProvider` is a `Type?` pointing to a class implementing `IPrerequisiteProvider`. The provider returns a list of `IPrerequisiteChecker` instances.

**Rationale**: Each checker has distinct detection logic (registry, file I/O, COM). An interface-based design makes them individually testable. The type reference keeps the scenario definition DRY — no checker instantiation in JSON.

**Alternatives considered**: `Func<Task<List<PrerequisiteResult>>>` delegates. Rejected because type reference is more discoverable and serializable.

### D4: Step 2→3 auto-skip with inline confirmation

**Decision**: When the step 2 (Switch Mode) `ActionExecuted` trigger fires, the tutorial auto-advances directly to step 4 (Command Mode), skipping the success confirmation step (step 3). A brief toast/overlay message ("Switched to Notepad!") replaces the full confirmation card.

**Rationale**: The confirmation step creates unnecessary friction — user is already in Notepad and must manually return to advance. Auto-skip keeps flow momentum. Step 5 remains as the final confirmation after command execution.

**Alternatives considered**: Keep step 3 but add a 5-second auto-advance timer. Rejected — still forces user to wait or manually interact. Full skip with toast is cleaner.

### D5: Slot-missing fallback via detection on step entry

**Decision**: When entering step 2 (or any `WaitForAction` step with `ActionExecuted` trigger), the `TutorialOrchestrator` checks whether the required slot exists in the current config. If missing, it shows an inline guidance card: "Your Pulsar slots are not configured yet. Go to Settings → Slots to add one, or click here to restore defaults."

**Rationale**: Prevent the tutorial from hanging indefinitely when slots are missing (e.g., user skipped setup wizard or deleted slots). Inline guidance is less disruptive than blocking the entire tutorial.

**Alternatives considered**: Block tutorial with error dialog. Rejected — too harsh for first-time users.

## Risks / Trade-offs

- **[Risk] Scenario JSON diverges from actual slot behavior**: If a scenario's JSON references slot "Insert Sample Text" but the generated slot uses a different label, the tutorial steps won't match reality. **Mitigation**: The tutorial step description references slot by localized label key, resolved at load time. Add integration test in `TutorialOnboardingFlowTests` that verifies label key resolution matches generated slot label.

- **[Risk] Prerequisite false negatives**: Registry-based detection may miss Office installations from the Microsoft Store or non-standard paths. **Mitigation**: Use multiple detection strategies per checker (registry + PATH + ProgramFiles). Mark uncertain results as `Recommended` severity, not `Required`.

- **[Risk] Fallback for missing VBA silently degrades experience**: If VBA is unavailable, Excel scenario falls back to sendkeys — user expects VBA demo but gets text insertion. **Mitigation**: Display a notice on the scenario card: "VBA not detected. Tutorial will use text insertion instead."

- **[Risk] Step 2→3 auto-skip confuses users**: User may not realize they successfully completed the switch step. **Mitigation**: Show a brief toast notification (3s) with checkmark + "Switched to Notepad!" before the command step appears.

- **[Risk] Registration of TutorialScenarioRegistry increases startup complexity**: Adding more singleton registrations. **Mitigation**: The registry is a simple in-memory collection (5 scenarios max) — no measurable startup impact.
