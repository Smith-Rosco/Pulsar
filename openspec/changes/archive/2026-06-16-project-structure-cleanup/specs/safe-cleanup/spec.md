## ADDED Requirements

### Requirement: Remove stale root-level documents
The project root SHALL contain only active project documents. Completed feature task/guide documents SHALL be moved to `Docs/archive/`.

#### Scenario: ARCHITECTURE_FIX_SUMMARY.md is moved
- **WHEN** inspecting the project root
- **THEN** `ARCHITECTURE_FIX_SUMMARY.md` SHALL NOT exist at the project root; it SHALL exist in `Docs/archive/`

#### Scenario: DYNAMIC_LAYOUT_TEST_GUIDE.md is moved
- **WHEN** inspecting the project root
- **THEN** `DYNAMIC_LAYOUT_TEST_GUIDE.md` SHALL NOT exist at the project root; it SHALL exist in `Docs/archive/`

#### Scenario: TODO_SLOTS_PER_PAGE.md is moved
- **WHEN** inspecting the project root
- **THEN** `TODO_SLOTS_PER_PAGE.md` SHALL NOT exist at the project root; it SHALL exist in `Docs/archive/`

### Requirement: Remove Python cache
The `Scripts/__pycache__/` directory SHALL be removed from the repository.

#### Scenario: Python cache directory is deleted
- **WHEN** inspecting `Scripts/`
- **THEN** `__pycache__/` SHALL NOT exist

### Requirement: Remove obsolete NativeMethods.txt
The file `Pulsar/Pulsar/NativeMethods.txt` SHALL be deleted. Its contents (COM interface method signatures) SHALL be considered obsolete — the project uses `PulsarNative.cs` and related files for native interop.

#### Scenario: NativeMethods.txt is deleted
- **WHEN** inspecting `Pulsar/Pulsar/`
- **THEN** `NativeMethods.txt` SHALL NOT exist

### Requirement: Merge duplicate TutorialSteps JSON files
The TutorialSteps JSON files in `Pulsar/Pulsar/Assets/` SHALL be moved to `Pulsar/Pulsar/Resources/Tutorial/` to eliminate duplication with the existing `Steps.*.json` files there.

#### Scenario: TutorialSteps files are in Resources/Tutorial/
- **WHEN** inspecting `Pulsar/Pulsar/Resources/Tutorial/`
- **THEN** `TutorialSteps.json`, `TutorialSteps.browser.json`, and `TutorialSteps.excel.json` SHALL exist there

#### Scenario: TutorialSteps files are removed from Assets/
- **WHEN** inspecting `Pulsar/Pulsar/Assets/`
- **THEN** `TutorialSteps*.json` SHALL NOT exist

### Requirement: Project builds after safe cleanup
All structural changes in this capability SHALL preserve the ability to build the project.

#### Scenario: Build passes after safe cleanup
- **WHEN** running `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- **THEN** the build SHALL succeed with zero errors
