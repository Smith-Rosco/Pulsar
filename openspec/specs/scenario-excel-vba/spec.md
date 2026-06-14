## ADDED Requirements

### Requirement: Excel scenario defines VBA script execution demo
The system SHALL define an "Excel Automation" tutorial scenario that demonstrates VbaRunnerPlugin in addition to WinSwitcher.

#### Scenario: Excel scenario has correct metadata
- **WHEN** the Excel scenario is defined in `TutorialScenarioRegistry`
- **THEN** its `Id` SHALL be `"excel"`
- **AND** its `StepsJsonPath` SHALL point to `Assets/TutorialSteps.excel.json`
- **AND** its `PrerequisiteProvider` SHALL be `ExcelPrerequisiteProvider`

#### Scenario: Excel scenario generates primary VBA slot
- **WHEN** `BuildInitialConfig()` runs with the Excel scenario and VBA is available
- **THEN** the command slots SHALL include a VbaRunner slot with `PluginId = "com.pulsar.vbarunner"`, `Action = "run"`, and `IsTutorialPrimary = true`

#### Scenario: Excel scenario generates secondary sendkeys slot
- **WHEN** `BuildInitialConfig()` runs with the Excel scenario
- **THEN** the command slots SHALL include a CommandPlugin sendkeys slot with `keys = "Hello from Pulsar!{ENTER}"` and `IsTutorialPrimary = false`

#### Scenario: Excel fallback uses sendkeys when VBA unavailable
- **WHEN** Excel is detected but VBA is not available
- **THEN** the primary command slot SHALL use `PluginId = "com.pulsar.command"`, `Action = "sendkeys"` instead of VbaRunner

### Requirement: Excel steps JSON mirrors 6-step structure
The `TutorialSteps.excel.json` file SHALL follow the same 6-step structure as `TutorialSteps.json` with Excel-specific copy.

#### Scenario: Step 2 references Excel slot
- **WHEN** a user sees step 2 in the Excel scenario
- **THEN** the instruction SHALL say "Press Ctrl+Q to open Switch Mode, hover over the Excel icon, then release"

#### Scenario: Step 4 references VBA demo slot
- **WHEN** a user sees step 4 in the Excel scenario
- **THEN** the instruction SHALL say "Press Ctrl+Shift+Q, hover over 'Run VBA Demo', then release to execute the script"
