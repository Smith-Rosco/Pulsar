## 1. Core Models & Registry

- [x] 1.1 Create `Models/Tutorial/TutorialScenario.cs` — class with `Id`, `TitleKey`, `DescriptionKey`, `SlotDescriptionKey`, `CommandSlotTemplates`, `PrerequisiteProvider`, `StepsJsonPath`
- [x] 1.2 Create `Models/Tutorial/CommandSlotTemplate.cs` — class with `PluginId`, `Action`, `Args`, `LabelKey`, `IconKey`, `IsTutorialPrimary`
- [x] 1.3 Create `Services/Tutorial/TutorialScenarioRegistry.cs` — singleton holding registered scenarios with `All`, `GetById()`, `Default`
- [x] 1.4 Register `TutorialScenarioRegistry` in `App.xaml.cs` as singleton
- [x] 1.5 Migrate existing `OnboardingUsageProfile` enum usage to `TutorialScenario` — wrap enum in registry, keep enum for backward compat

## 2. Prerequisite Detection System

- [x] 2.1 Create `Services/Tutorial/Prerequisites/IPrerequisiteChecker.cs` — interface with `Id`, `DisplayNameKey`, `Severity`, `CheckAsync()`
- [x] 2.2 Create `Services/Tutorial/Prerequisites/PrerequisiteResult.cs` — result model with `Id`, `DisplayNameKey`, `Severity`, `Status`, `Details`
- [x] 2.3 Create `Services/Tutorial/Prerequisites/PrerequisiteStatus.cs` — enum: `Pending`, `Met`, `NotMet`, `Unknown`
- [x] 2.4 Create `Services/Tutorial/Prerequisites/PrerequisiteSeverity.cs` — enum: `Required`, `Recommended`, `RuntimeOnly`
- [x] 2.5 Create `Services/Tutorial/Prerequisites/IPrerequisiteProvider.cs` — interface with `GetCheckersAsync()`, `CheckAllAsync()`
- [x] 2.6 Create `Services/Tutorial/Prerequisites/ExcelExistsChecker.cs` — check `HKCR\Excel.Application\CurVer` registry key
- [x] 2.7 Create `Services/Tutorial/Prerequisites/VbaSupportChecker.cs` — check `VBE7.DLL` existence in Office directory
- [x] 2.8 Create `Services/Tutorial/Prerequisites/BrowserExistsChecker.cs` — check `chrome.exe` and `msedge.exe` in PATH
- [x] 2.9 Create `Services/Tutorial/Prerequisites/ExcelPrerequisiteProvider.cs` — aggregates `ExcelExistsChecker` + `VbaSupportChecker`
- [x] 2.10 Create `Services/Tutorial/Prerequisites/BrowserPrerequisiteProvider.cs` — aggregates `BrowserExistsChecker`
- [x] 2.11 Register prerequisite services in `App.xaml.cs`

## 3. Multi-Slot Profile Generation

- [x] 3.1 Modify `OnboardingTemplateService.BuildCommandExample()` → `BuildCommandExamples()` — return `List<PluginSlot>` based on `TutorialScenario.CommandSlotTemplates`
- [x] 3.2 Modify `OnboardingTemplateService.BuildInitialConfig()` — use `BuildCommandExamples()` instead of single slot
- [x] 3.3 Add VbaRunner demo script file to `Assets/Scripts/Demo/excel_demo.txt` — simple VBA script for tutorial
- [x] 3.4 Update `ConfigPreviewSummary` to show total command slot count from scenario templates
- [x] 3.5 Update `OnboardingTemplateService.BuildPreviewSummary()` — accept `TutorialScenario` and count primary + secondary slots

## 4. Scenario-Based TutorialSteps JSON

- [x] 4.1 Create `Assets/TutorialSteps.excel.json` — 6 steps, references Excel and "Run VBA Demo" slot
- [x] 4.2 Create `Assets/TutorialSteps.browser.json` — 6 steps, references browser and bookmarklet slot (P1)
- [x] 4.3 Modify `TutorialStepLoader` — accept optional `scenarioId`, route to `Assets/TutorialSteps.{scenarioId}.json`
- [x] 4.4 Wire `TutorialOrchestrator` to pass scenario ID from `TutorialScenarioRegistry` to `TutorialStepLoader`
- [x] 4.5 Add integration test: each scenario's JSON loads and deserializes correctly

## 5. Step 2→3 Transition & Slot-Missing Fallback

- [x] 5.1 Add toast notification component to `TutorialOverlayWindow` — brief overlay message with checkmark
- [x] 5.2 Modify `TutorialOrchestrator.NextStepAsync()` — detect step 2 → step 3 bypass: when ActionExecuted/Switch fires, skip step 3, show toast, advance to step 4
- [x] 5.3 Add slot existence check in `TutorialOrchestrator.ShowStepAsync()` — when entering WaitForAction step with ActionExecuted trigger, validate target slot exists
- [x] 5.4 Create inline guidance card UI — "Slots not configured" message with link to Settings
- [x] 5.5 Wire guidance card to display when slot check fails, keep Next button visible
- [x] 5.6 Add trigger re-check: when slot is added while guidance is shown, auto-advance via SlotAdded trigger
- [x] 5.7 Update `TutorialStepCard.xaml` — support dynamic mode for guidance vs normal display

## 6. Setup Wizard UI Integration

- [x] 6.1 Modify `FirstLaunchSetupWizardViewModel` — load scenarios from `TutorialScenarioRegistry`, run prerequisite checks on selection change
- [x] 6.2 Add `PrerequisiteResults` observable property to ViewModel — per-scenario dict of check results
- [x] 6.3 Add `CanFinish` validation — disable Finish when Required prerequisite is NotMet
- [x] 6.4 Update `FirstLaunchSetupWizardContent.xaml` — add prerequisite status indicators per scenario card
- [x] 6.5 Add status indicator styles + converters (✅/⚠️/🛑/⏳ based on PrerequisiteStatus)
- [x] 6.6 Wire Finish command to pass selected `TutorialScenario` to `OnboardingTemplateService.BuildInitialConfig()`
- [x] 6.7 Wire tutorial start to use selected scenario's step file via `TutorialStepLoader`

## 7. Localization

- [x] 7.1 Add EN resource entries: scenario titles, descriptions, prerequisite labels (~20 entries)
- [x] 7.2 Add zh-CN resource entries: matching translations (~20 entries)
- [x] 7.3 Add toast notification strings for step 2→3 skip ("Switched to {app}!")
- [x] 7.4 Add slot-missing guidance string ("Go to Settings → Slots to configure")

## 8. Tests

- [x] 8.1 Unit test: `TutorialScenarioRegistry` returns correct scenarios by ID
- [x] 8.2 Unit test: `ExcelExistsChecker` returns Met/NotMet based on registry state
- [x] 8.3 Unit test: `VbaSupportChecker` returns Met/NotMet based on DLL existence
- [x] 8.4 Unit test: `OnboardingTemplateService.BuildCommandExamples()` generates correct slot count per scenario
- [x] 8.5 Unit test: `TutorialStepLoader` loads correct JSON per scenario ID
- [x] 8.6 Integration test: `TutorialOrchestrator` skips step 3 on ActionExecuted/Switch trigger
- [x] 8.7 Integration test: slot-missing fallback shows guidance card and advances on slot add
- [x] 8.8 Integration test: `FirstLaunchSetupWizardViewModel` blocks Finish when Required prerequisite NotMet
- [x] 8.9 Run full `dotnet test` — confirm no regressions
