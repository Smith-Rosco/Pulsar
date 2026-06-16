## ADDED Requirements

### Requirement: Merge Versioning into Metadata
The files in `Core/Plugin/Versioning/` SHALL be moved into `Core/Plugin/Metadata/`. Their namespaces SHALL be updated from `Pulsar.Core.Plugin.Versioning` to `Pulsar.Core.Plugin.Metadata`.

#### Scenario: Versioning files are in Metadata directory
- **WHEN** inspecting `Pulsar/Pulsar/Core/Plugin/Metadata/`
- **THEN** the following files SHALL exist: `PluginManifest.cs`, `PluginManifestLoader.cs`, `PluginVersionResolver.cs`

#### Scenario: Versioning directory is removed
- **WHEN** inspecting `Pulsar/Pulsar/Core/Plugin/`
- **THEN** `Versioning/` SHALL NOT exist

#### Scenario: Namespaces are updated
- **WHEN** inspecting each moved `.cs` file
- **THEN** the namespace SHALL be `Pulsar.Core.Plugin.Metadata`

#### Scenario: No stale namespace references remain
- **WHEN** searching the codebase for `Pulsar.Core.Plugin.Versioning`
- **THEN** zero references SHALL be found

### Requirement: Remove documentation/example code from Dependencies/
The files `Core/Plugin/Dependencies/README.md` and `Core/Plugin/Dependencies/DependencyIsolationExample.cs` SHALL be deleted. These files are documentation and example code, not runtime components.

#### Scenario: README.md is deleted
- **WHEN** inspecting `Pulsar/Pulsar/Core/Plugin/Dependencies/`
- **THEN** `README.md` SHALL NOT exist

#### Scenario: DependencyIsolationExample.cs is deleted
- **WHEN** inspecting `Pulsar/Pulsar/Core/Plugin/Dependencies/`
- **THEN** `DependencyIsolationExample.cs` SHALL NOT exist

#### Scenario: No external references to deleted files
- **WHEN** searching the codebase for `DependencyIsolationExample`
- **THEN** zero references SHALL be found

### Requirement: Update AGENTS.md references
The `AGENTS.md` file SHALL be updated to reflect:
- Removed `Versioning/` directory path
- Consolidated Plugin metadata location
- Any path references changed in this capability

#### Scenario: AGENTS.md contains accurate paths
- **WHEN** searching `AGENTS.md` for old paths (`Core/Plugin/Versioning/`)
- **THEN** references SHALL point to the new consolidated location

### Requirement: Project builds after infrastructure cleanup
All file moves and namespace changes SHALL preserve the ability to build the project.

#### Scenario: Build passes after infrastructure cleanup
- **WHEN** running `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- **THEN** the build SHALL succeed with zero errors
