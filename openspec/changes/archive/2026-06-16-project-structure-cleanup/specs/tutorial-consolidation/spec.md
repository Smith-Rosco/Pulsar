## ADDED Requirements

### Requirement: Create consolidated Tutorial feature directory
The `Pulsar/Pulsar/Features/Tutorial/` directory SHALL be created with subdirectories: `Services/`, `Views/`, `Models/`, `Helpers/`, `Data/`.

#### Scenario: Feature directory structure exists
- **WHEN** inspecting `Pulsar/Pulsar/Features/Tutorial/`
- **THEN** the following subdirectories SHALL exist: `Services/`, `Views/`, `Models/`, `Helpers/`, `Data/`

### Requirement: Move Tutorial files into Features/Tutorial/
All files currently spread across Tutorial-related directories SHALL be moved to the consolidated directory as follows:

| Source | Destination |
|--------|-------------|
| `Helpers/Tutorial/*` | `Features/Tutorial/Helpers/` |
| `Models/Tutorial/*` | `Features/Tutorial/Models/` |
| `Views/Tutorial/*` | `Features/Tutorial/Views/` |
| `Services/Tutorial/*` (excluding Prerequisites/ and TriggerHandlers/) | `Features/Tutorial/Services/` |
| `Services/Tutorial/Prerequisites/*` | `Features/Tutorial/Services/Prerequisites/` |
| `Services/Tutorial/TriggerHandlers/*` | `Features/Tutorial/Services/TriggerHandlers/` |

#### Scenario: Helpers/Tutorial/ is empty
- **WHEN** inspecting `Pulsar/Pulsar/Helpers/Tutorial/`
- **THEN** the directory SHALL NOT contain any non-hidden files

#### Scenario: Models/Tutorial/ is empty
- **WHEN** inspecting `Pulsar/Pulsar/Models/Tutorial/`
- **THEN** the directory SHALL NOT contain any non-hidden files

#### Scenario: Views/Tutorial/ is empty
- **WHEN** inspecting `Pulsar/Pulsar/Views/Tutorial/`
- **THEN** the directory SHALL NOT contain any non-hidden files

#### Scenario: Services/Tutorial/ is empty
- **WHEN** inspecting `Pulsar/Pulsar/Services/Tutorial/`
- **THEN** the directory SHALL NOT contain any non-hidden files

#### Scenario: Files are in Features/Tutorial/
- **WHEN** listing `Pulsar/Pulsar/Features/Tutorial/` recursively
- **THEN** all 40+ tutorial-related files SHALL be present

### Requirement: Update namespaces
All C# files moved to `Features/Tutorial/` SHALL have their namespaces updated from the old paths (e.g., `Pulsar.Helpers.Tutorial`, `Pulsar.Models.Tutorial`, `Pulsar.Views.Tutorial`, `Pulsar.Services.Tutorial`) to `Pulsar.Features.Tutorial.*`.

#### Scenario: Namespaces match new directory structure
- **WHEN** inspecting each moved `.cs` file in `Features/Tutorial/`
- **THEN** its namespace SHALL match its new directory location under `Pulsar.Features.Tutorial`

### Requirement: Update all cross-references
All files outside `Features/Tutorial/` that reference the old Tutorial namespaces SHALL be updated to use the new namespaces.

#### Scenario: No references to old Tutorial namespaces remain
- **WHEN** searching the codebase (excluding `Features/Tutorial/` itself)
- **THEN** there SHALL be zero references to old Tutorial namespaces (e.g., `Pulsar.Helpers.Tutorial`, `Pulsar.Models.Tutorial`, `Pulsar.Views.Tutorial`, `Pulsar.Services.Tutorial`)

### Requirement: Move TutorialSteps JSON to Data/ subdirectory
The JSON step files currently in `Resources/Tutorial/` SHALL be moved or symlinked to `Features/Tutorial/Data/`.

#### Scenario: Tutorial JSON files are in Data/
- **WHEN** inspecting `Pulsar/Pulsar/Features/Tutorial/Data/`
- **THEN** `Steps.en.json` and `Steps.zh-CN.json` SHALL exist

### Requirement: XAML resource references preserved
Any XAML files that reference Tutorial-related resources by their old paths SHALL be updated to the new paths.

#### Scenario: No stale XAML resource references
- **WHEN** searching `.xaml` files for old Tutorial paths
- **THEN** there SHALL be zero stale references

### Requirement: Project builds after tutorial consolidation
All namespace and path changes SHALL preserve the ability to build the project.

#### Scenario: Build passes after consolidation
- **WHEN** running `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- **THEN** the build SHALL succeed with zero errors

#### Scenario: Tests pass after consolidation
- **WHEN** running `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj`
- **THEN** all tests SHALL pass
