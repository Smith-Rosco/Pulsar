## ADDED Requirements

### Requirement: Prerequisite checkers report software availability
The system SHALL define an `IPrerequisiteChecker` interface with per-checker detection logic and a result status.

#### Scenario: Checker returns Met status
- **WHEN** `CheckAsync()` is called and the prerequisite is available
- **THEN** the result SHALL have `Status = Met`

#### Scenario: Checker returns NotMet status
- **WHEN** `CheckAsync()` is called and the prerequisite is not available
- **THEN** the result SHALL have `Status = NotMet`

#### Scenario: Checker has severity level
- **WHEN** a `PrerequisiteResult` is created
- **THEN** it SHALL have a `Severity` value: `Required`, `Recommended`, or `RuntimeOnly`

### Requirement: Built-in checkers for Excel, VBA, and browser detection
The system SHALL provide built-in `IPrerequisiteChecker` implementations for common scenarios.

#### Scenario: Excel checker detects installed Excel
- **WHEN** `ExcelExistsChecker.CheckAsync()` is called
- **THEN** it SHALL check the Windows Registry at `HKCR\Excel.Application\CurVer`
- **AND** return `Met` if the registry key exists
- **AND** return `NotMet` otherwise

#### Scenario: VBA checker detects VBE7.DLL
- **WHEN** `VbaSupportChecker.CheckAsync()` is called
- **THEN** it SHALL check for `VBE7.DLL` in the Office installation directory
- **AND** return `Met` if the file exists
- **AND** return `NotMet` otherwise
- **AND** its severity SHALL be `Recommended` (not `Required`)

#### Scenario: Browser checker detects Chrome or Edge
- **WHEN** `BrowserExistsChecker.CheckAsync()` is called
- **THEN** it SHALL check for `chrome.exe` in PATH and `msedge.exe` in PATH
- **AND** return `Met` if at least one browser is found
- **AND** return a `Details` field listing which browsers were detected

### Requirement: Prerequisite provider aggregates multiple checkers
The system SHALL define an `IPrerequisiteProvider` interface that returns a list of `IPrerequisiteChecker` instances for a given scenario.

#### Scenario: Provider returns all checkers for a scenario
- **WHEN** `ExcelPrerequisiteProvider.GetCheckersAsync()` is called
- **THEN** it SHALL return at least `ExcelExistsChecker` and `VbaSupportChecker`

#### Scenario: Provider runs all checkers and returns results
- **WHEN** `ExcelPrerequisiteProvider.CheckAllAsync()` is called
- **THEN** it SHALL invoke `CheckAsync()` on each contained checker
- **AND** return a list of `PrerequisiteResult` with one entry per checker

### Requirement: Prerequisite status affects UI rendering
The setup wizard SHALL display prerequisite status per scenario card, using visual indicators.

#### Scenario: Met status shows green checkmark
- **WHEN** a prerequisite has `Status = Met`
- **THEN** the UI SHALL display a green ✅ indicator

#### Scenario: NotMet Required status shows red block
- **WHEN** a prerequisite has `Status = NotMet` and `Severity = Required`
- **THEN** the UI SHALL display a red 🛑 indicator
- **AND** the Finish button SHALL be disabled for that scenario

#### Scenario: NotMet Recommended status shows orange warning
- **WHEN** a prerequisite has `Status = NotMet` and `Severity = Recommended`
- **THEN** the UI SHALL display an orange ⚠️ indicator
- **AND** the Finish button SHALL remain enabled (scenario still selectable)

#### Scenario: Pending status shows loading spinner
- **WHEN** a prerequisite check is in progress
- **THEN** the UI SHALL display a ⏳ indicator

### Requirement: Scenario fallback when prerequisite partially met
When a scenario's Required prerequisites are met but Recommended prerequisites are not, the system SHALL generate a modified slot configuration as fallback.

#### Scenario: VBA unavailable falls back to sendkeys
- **WHEN** Excel is detected but `VbaSupportChecker` returns `NotMet`
- **THEN** the Excel scenario SHALL generate a CommandPlugin sendkeys slot instead of a VbaRunner slot
- **AND** the UI SHALL display a notice: "VBA not detected. Tutorial will use text insertion."

#### Scenario: Browser unavailable marks scenario unreachable
- **WHEN** no supported browser is detected
- **THEN** the browser scenario SHALL show all Required prerequisites as NotMet
- **AND** the scenario card SHALL be visually dimmed and unselectable
