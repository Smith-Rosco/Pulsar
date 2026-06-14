## ADDED Requirements

### Requirement: TutorialScenario model defines per-scenario metadata
The system SHALL define a `TutorialScenario` class that bundles all configuration for a tutorial scenario: identifier, localized display strings, command slot templates, prerequisite provider reference, and step file path.

#### Scenario: Scenario contains all required metadata
- **WHEN** a `TutorialScenario` instance is constructed
- **THEN** it SHALL expose `Id`, `TitleKey`, `DescriptionKey`, `SlotDescriptionKey`, `CommandSlotTemplates` (list of `CommandSlotTemplate`), `PrerequisiteProvider` (optional `Type`), and `StepsJsonPath` (optional `string`)

#### Scenario: Scenario can define multiple command slots
- **WHEN** a `TutorialScenario` is defined with multiple `CommandSlotTemplate` entries
- **THEN** exactly one slot SHALL be marked `IsTutorialPrimary = true`
- **AND** the remaining slots SHALL be generated alongside the primary slot

#### Scenario: Default scenario fallback
- **WHEN** `StepsJsonPath` is null
- **THEN** the system SHALL load `Assets/TutorialSteps.json` (the default Notepad flow)

### Requirement: TutorialScenarioRegistry holds all registered scenarios
The system SHALL register all available `TutorialScenario` instances in a `TutorialScenarioRegistry` singleton, providing lookup by ID and enumeration.

#### Scenario: Registry returns all scenarios
- **WHEN** `TutorialScenarioRegistry.All` is accessed
- **THEN** it SHALL return all registered scenarios

#### Scenario: Registry looks up scenario by ID
- **WHEN** `TutorialScenarioRegistry.GetById("excel")` is called
- **THEN** it SHALL return the Excel scenario if registered
- **AND** return null if no scenario matches the given ID

#### Scenario: Registry returns a default scenario
- **WHEN** `TutorialScenarioRegistry.Default` is accessed
- **THEN** it SHALL return the first registered scenario

### Requirement: TutorialStepLoader routes to per-scenario step files
The `TutorialStepLoader` SHALL accept an optional `scenarioId` parameter and load the corresponding JSON file.

#### Scenario: Default loads TutorialSteps.json
- **WHEN** `LoadStepsAsync()` is called with no `scenarioId`
- **THEN** the system SHALL load and return steps from `Assets/TutorialSteps.json`

#### Scenario: Scenario ID routes to specific file
- **WHEN** `LoadStepsAsync("excel")` is called
- **THEN** the system SHALL load steps from `Assets/TutorialSteps.excel.json`

#### Scenario: Unknown scenario ID falls back to default
- **WHEN** `LoadStepsAsync("unknown")` is called
- **THEN** the system SHALL fall back to `Assets/TutorialSteps.json`

### Requirement: BuildInitialConfig generates multiple command slots per scenario
The `OnboardingTemplateService.BuildInitialConfig()` method SHALL accept a `TutorialScenario` and generate all `CommandSlotTemplate` entries as `PluginSlot` instances.

#### Scenario: Primary slot appears in generated config
- **WHEN** `BuildInitialConfig()` generates command slots for a scenario with 2 slot templates
- **THEN** the generated config SHALL contain exactly 2 command slots in the Global profile's CommandMode list

#### Scenario: Generated slots have correct plugin IDs and actions
- **WHEN** `BuildInitialConfig()` generates a slot from a template with `PluginId = "com.pulsar.vbarunner"` and `Action = "run"`
- **THEN** the generated `PluginSlot` SHALL have matching `PluginId` and `Action` values
