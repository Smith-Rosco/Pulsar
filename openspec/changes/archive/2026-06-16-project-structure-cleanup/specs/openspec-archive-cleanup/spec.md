## ADDED Requirements

### Requirement: Remove completed changes from March 2026
Completed openspec change directories from March 2026 (`openspec/changes/archive/2026-03-*`) SHALL be removed from the repository. Each change SHALL be verified to have a corresponding implementation commit before deletion.

#### Scenario: March 2026 changes are removed
- **WHEN** inspecting `openspec/changes/archive/`
- **THEN** no directories matching `2026-03-*` SHALL exist

### Requirement: Remove completed changes from April 2026
Completed openspec change directories from April 2026 (`openspec/changes/archive/2026-04-*`) SHALL be removed from the repository. Each change SHALL be verified to have a corresponding implementation commit before deletion.

#### Scenario: April 2026 changes are removed
- **WHEN** inspecting `openspec/changes/archive/`
- **THEN** no directories matching `2026-04-*` SHALL exist

### Requirement: Preserve recent changes (June 2026)
Openspec changes from June 2026 SHALL be preserved. These are recent enough to potentially be referenced during ongoing development.

#### Scenario: June 2026 changes are preserved
- **WHEN** inspecting `openspec/changes/archive/`
- **THEN** directories matching `2026-06-*` SHALL still exist

### Requirement: Project builds after archive cleanup
File deletions SHALL NOT affect any source code files.

#### Scenario: Build passes after archive cleanup
- **WHEN** running `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- **THEN** the build SHALL succeed with zero errors
